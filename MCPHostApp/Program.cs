using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// Create the MCP client outside of DI to avoid disposal issues.
// This launches the MCP server using npx (requires Node.js and npx in the Linux container).
// The server is started in the /workspace/test-files directory, exposing file tools.
var mcpClientTask = McpClientFactory.CreateAsync(
    new StdioClientTransport(new()
    {
        Command = "npx",
        Arguments = ["-y", "@modelcontextprotocol/server-filesystem", "."],
        Name = "Files MCP Server",
        WorkingDirectory = "/workspace/test-files"
    }));

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// Register the Azure OpenAI chat client as a singleton.
// This uses the endpoint and deployment name from configuration/environment variables.
builder.Services.AddSingleton<IChatClient>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    return new ChatClientBuilder(
        new AzureOpenAIClient(
            new Uri(configuration["AZURE_OPENAI_ENDPOINT"]!),
            new DefaultAzureCredential())
        .GetChatClient(configuration["AZURE_MODEL_DEPLOYMENT"]).AsIChatClient())
    .UseFunctionInvocation()
    .Build();
});

// Register MCP client as a simple singleton
var mcpClient = await mcpClientTask;
builder.Services.AddSingleton<IMcpClient>(mcpClient);

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map the SignalR chat hub endpoint
app.MapHub<ChatHub>("/chathub");

app.Run();
