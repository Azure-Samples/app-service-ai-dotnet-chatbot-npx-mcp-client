using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

/// <summary>
/// SignalR hub for chat and tool discovery, integrating an MCP client (invoked via npx) as a tool provider.
/// </summary>
public class ChatHub : Hub
{
    // The chat client that handles conversation and streaming responses (e.g., to OpenAI or other LLMs)
    private readonly IChatClient _chatClient;
    // The MCP client, which wraps the npx-invoked MCP server and exposes its tools
    private readonly IMcpClient _mcpClient;

    public ChatHub(IChatClient chatClient, IMcpClient mcpClient)
    {
        _chatClient = chatClient;
        _mcpClient = mcpClient;
    }

    /// <summary>
    /// Handles incoming chat messages from the client, streams LLM responses, and integrates MCP tools.
    /// </summary>
    public async Task SendMessage(string user, string message, List<object> conversationHistory)
    {
        try
        {
            // Get available tools from MCP server
            IList<McpClientTool> tools = await _mcpClient.ListToolsAsync();

            List<ChatMessage> messages = new();
            foreach (var item in conversationHistory)
            {
                if (item is Dictionary<string, object> dict)
                {
                    var role = dict.GetValueOrDefault("role")?.ToString();
                    var content = dict.GetValueOrDefault("content")?.ToString();
                    if (!string.IsNullOrEmpty(role) && !string.IsNullOrEmpty(content))
                    {
                        messages.Add(new ChatMessage(
                            role.Equals("user", StringComparison.OrdinalIgnoreCase) ? ChatRole.User : ChatRole.Assistant,
                            content));
                    }
                }
            }

            if (messages.Count == 0)
            {
                messages.Add(new ChatMessage(ChatRole.System,
                    "You are a helpful assistant with access to file operations through MCP tools. " +
                    "The filesystem server has access to /workspace/test-files directory. " +
                    "When users ask to list files or access files, use the available MCP tools like list_directory, read_file, etc. " +
                    "If you get permission errors, guide the user to work within the allowed directory structure."));
            }

            // Add the new user message
            messages.Add(new ChatMessage(ChatRole.User, message));

            // Notify all clients that the assistant is "typing"
            await Clients.All.SendAsync("TypingIndicator", true);

            string fullResponse = "";
            List<ChatResponseUpdate> updates = [];

            // Stream the LLM response, passing the available MCP tools for tool-calling
            await foreach (ChatResponseUpdate update in _chatClient
                .GetStreamingResponseAsync(messages, new() { Tools = [.. tools] }))
            {
                fullResponse += update.Text;
                updates.Add(update);
                // Send incremental updates to the client UI
                await Clients.All.SendAsync("ReceiveMessageStream", update.Text ?? "");
            }

            // Stop the typing indicator
            await Clients.All.SendAsync("TypingIndicator", false);

            // Update the conversation with all messages, including tool calls
            messages.AddMessages(updates);
        }
        catch (Exception ex)
        {
            await Clients.All.SendAsync("TypingIndicator", false);
            await Clients.All.SendAsync("ReceiveMessage", "System", $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the list of available MCP tools to the client.
    /// </summary>
    public async Task GetAvailableTools()
    {
        try
        {
            IList<McpClientTool> tools = await _mcpClient.ListToolsAsync();
            var toolNames = tools.Select(t => t.Name).ToList();
            await Clients.Caller.SendAsync("ReceiveAvailableTools", toolNames);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "System", $"Error getting tools: {ex.Message}");
        }
    }
}
