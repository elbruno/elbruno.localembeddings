using ElBruno.ModelContextProtocol.MCPToolRouter;

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║          MCP Tool Router Sample Application                    ║");
Console.WriteLine("║    Semantic tool discovery with local embeddings              ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// =============================================================================
// Step 1: Create mock MCP tool definitions
// =============================================================================
Console.WriteLine("Step 1: Creating MCP tool definitions...");
Console.WriteLine();

var tools = new List<McpTool>
{
    new McpTool
    {
        Name = "search_web",
        Description = "Search the internet for information using a search engine. Returns relevant web pages and snippets matching the search query. Use this when you need to find current information, news, or general knowledge from the web.",
        Parameters = new { query = "The search query string", max_results = 10 }
    },
    
    new McpTool
    {
        Name = "send_email",
        Description = "Send an email message to one or more recipients. Supports plain text and HTML formatting, attachments, CC, and BCC. Use this when you need to communicate via email or send notifications.",
        Parameters = new { to = "Recipient email address(es)", subject = "Email subject line", body = "Email content" }
    },
    
    new McpTool
    {
        Name = "create_file",
        Description = "Create a new file on the local filesystem with specified content. Supports text files, JSON, XML, and binary formats. Use this when you need to persist data or generate output files.",
        Parameters = new { path = "File path", content = "File content", encoding = "utf-8" }
    },
    
    new McpTool
    {
        Name = "analyze_data",
        Description = "Perform statistical analysis and data processing on datasets. Supports descriptive statistics, correlation analysis, and data visualization. Use this for data science tasks and numerical analysis.",
        Parameters = new { dataset = "Input data", analysis_type = "Type of analysis to perform" }
    },
    
    new McpTool
    {
        Name = "translate_text",
        Description = "Translate text from one language to another using machine translation. Supports over 100 languages including English, Spanish, French, German, Chinese, Japanese, and more. Use this for multilingual communication.",
        Parameters = new { text = "Text to translate", source_lang = "Source language code", target_lang = "Target language code" }
    },
    
    new McpTool
    {
        Name = "generate_image",
        Description = "Generate images from text descriptions using AI image generation models. Create illustrations, artwork, diagrams, and visual content from natural language prompts.",
        Parameters = new { prompt = "Image description", style = "Art style", resolution = "Image resolution" }
    },
    
    new McpTool
    {
        Name = "query_database",
        Description = "Execute SQL queries against relational databases. Supports SELECT, INSERT, UPDATE, and DELETE operations. Connect to MySQL, PostgreSQL, SQL Server, and SQLite databases.",
        Parameters = new { connection_string = "Database connection", query = "SQL query string" }
    },
    
    new McpTool
    {
        Name = "convert_format",
        Description = "Convert files and data between different formats. Supports document conversion (PDF, DOCX, HTML), image conversion (PNG, JPEG, WebP), and data format conversion (JSON, XML, CSV, YAML).",
        Parameters = new { input_file = "Input file path", output_format = "Target format" }
    },
    
    new McpTool
    {
        Name = "schedule_task",
        Description = "Schedule tasks and reminders to run at specified times or intervals. Set up recurring jobs, one-time reminders, and cron-style scheduled operations. Use this for automation and task management.",
        Parameters = new { task_name = "Task identifier", schedule = "Cron expression or time", action = "Task to perform" }
    },
    
    new McpTool
    {
        Name = "compress_archive",
        Description = "Create compressed archives (ZIP, TAR, GZIP) from files and folders. Supports encryption, compression level settings, and multi-file archiving. Use this for file packaging and backup operations.",
        Parameters = new { source_paths = "Files/folders to compress", output_path = "Archive file path", format = "Archive format" }
    }
};

Console.WriteLine($"Created {tools.Count} MCP tool definitions:");
foreach (var tool in tools)
{
    Console.WriteLine($"  • {tool.Name} - {tool.Description[..Math.Min(60, tool.Description.Length)]}...");
}
Console.WriteLine();

// =============================================================================
// Step 2: Initialize the ToolRouter with embeddings
// =============================================================================
Console.WriteLine("Step 2: Initializing ToolRouter with local embeddings...");
Console.WriteLine("  (Embedding model will auto-download on first run)");
Console.WriteLine();

var startTime = DateTime.Now;

// Create the tool router with default embedding model
var router = await ToolRouter.CreateAsync(tools);

var initTime = DateTime.Now - startTime;
Console.WriteLine($"✓ ToolRouter initialized in {initTime.TotalSeconds:F2} seconds");
Console.WriteLine($"  Indexed {tools.Count} tools");
Console.WriteLine($"  Embedding model: sentence-transformers/all-MiniLM-L6-v2");
Console.WriteLine();

// =============================================================================
// Step 3: Demonstrate semantic tool routing
// =============================================================================
Console.WriteLine("Step 3: Testing semantic tool routing...");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var queries = new[]
{
    "I need to find information online about machine learning",
    "Send a message to my team about the project update",
    "Save this data to a text document",
    "Calculate statistics on this sales data",
    "Convert this document to Spanish",
    "Create a picture of a sunset over mountains",
    "Get customer records from the database",
    "Change this PDF into a Word document",
    "Remind me to call the client tomorrow at 2pm",
    "Zip these files for backup"
};

foreach (var query in queries)
{
    Console.WriteLine($"Query: \"{query}\"");
    
    // Route the query to the most relevant tool
    var routeStartTime = DateTime.Now;
    var result = await router.RouteAsync(query, topK: 3);
    var routeTime = DateTime.Now - routeStartTime;
    
    Console.WriteLine($"  Routing time: {routeTime.TotalMilliseconds:F1}ms");
    Console.WriteLine();
    Console.WriteLine("  Top 3 matching tools:");
    
    for (var i = 0; i < result.Count && i < 3; i++)
    {
        var match = result[i];
        var scoreBar = new string('█', (int)(match.Score * 20));
        var emptyBar = new string('░', 20 - (int)(match.Score * 20));
        
        Console.WriteLine($"    {i + 1}. [{scoreBar}{emptyBar}] {match.Score:F4} - {match.Tool.Name}");
        
        if (i == 0)
        {
            var preview = match.Tool.Description.Length > 80 
                ? match.Tool.Description[..77] + "..." 
                : match.Tool.Description;
            Console.WriteLine($"       {preview}");
        }
    }
    
    Console.WriteLine();
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    Console.WriteLine();
}

// =============================================================================
// Step 4: Interactive mode
// =============================================================================
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine("Interactive Mode - Test your own queries!");
Console.WriteLine("═══════════════════════════════════════════════════════════════");
Console.WriteLine();

while (true)
{
    Console.Write("Describe what you want to do (or 'exit' to quit): ");
    var userQuery = Console.ReadLine();
    
    if (string.IsNullOrWhiteSpace(userQuery) || userQuery.Trim().ToLowerInvariant() == "exit")
    {
        Console.WriteLine("\nGoodbye!");
        break;
    }
    
    Console.WriteLine();
    
    // Route the user's query
    var userResult = await router.RouteAsync(userQuery, topK: 3);
    
    if (userResult.Count > 0)
    {
        var bestMatch = userResult[0];
        Console.WriteLine($"✓ Best match: {bestMatch.Tool.Name} (confidence: {bestMatch.Score:P1})");
        Console.WriteLine($"  {bestMatch.Tool.Description}");
        Console.WriteLine();
        
        if (userResult.Count > 1)
        {
            Console.WriteLine("  Alternative tools:");
            for (var i = 1; i < Math.Min(3, userResult.Count); i++)
            {
                Console.WriteLine($"    • {userResult[i].Tool.Name} ({userResult[i].Score:P1})");
            }
            Console.WriteLine();
        }
    }
    else
    {
        Console.WriteLine("⚠ No matching tools found.");
        Console.WriteLine();
    }
}

Console.WriteLine();
Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║              MCP Tool Router Sample Complete!                  ║");
Console.WriteLine("║                                                                ║");
Console.WriteLine("║  This sample demonstrated:                                     ║");
Console.WriteLine("║  ✓ Semantic tool indexing with local embeddings                ║");
Console.WriteLine("║  ✓ Natural language tool discovery                             ║");
Console.WriteLine("║  ✓ Similarity-based routing with confidence scores             ║");
Console.WriteLine("║  ✓ Fast routing (~10ms per query)                              ║");
Console.WriteLine("║  ✓ No cloud dependencies - 100% local                          ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
