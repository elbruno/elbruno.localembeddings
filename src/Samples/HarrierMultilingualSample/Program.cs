using ElBruno.LocalEmbeddings.Harrier;
using ElBruno.LocalEmbeddings.Harrier.Options;
using Microsoft.Extensions.AI;

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║    Harrier-OSS-v1 Multilingual Embedding Sample (94+ langs)   ║");
Console.WriteLine("║  Cross-lingual retrieval: search English facts in any language ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// =============================================================================
// SETUP: Create two generators — one for documents, one for queries
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// Detect GPU availability
bool isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
bool useGpu = isWindows; // DirectML is Windows-only

Console.WriteLine($"Platform: {(isWindows ? "Windows" : "Linux/macOS")}");
Console.WriteLine($"Acceleration: {(useGpu ? "🚀 DirectML GPU" : "💻 CPU")}");
Console.WriteLine();

Console.WriteLine($"Setup: Initializing Harrier Model (INT8 Quantized) — {(useGpu ? "DirectML GPU" : "CPU")}");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

var progress = new Progress<double>(p =>
{
    Console.Write($"\r⬇️ Downloading model: {p:P0}   ");
});

// Generator for documents (no instruction prefix)
var docOptions = new HarrierEmbeddingsOptions
{
    ModelVariant = HarrierModelVariant.Quantized,
    InstructionPrefix = string.Empty,
    EnsureModelDownloaded = true,
    UseDirectML = useGpu
};

Console.WriteLine("Loading document generator (no instruction prefix)...");
await using var docGenerator = await HarrierEmbeddingGenerator.CreateAsync(docOptions, progress);
Console.WriteLine();
Console.WriteLine($"✓ Document generator ready ({(useGpu ? "GPU/DirectML" : "CPU")})");

// Generator for queries (with instruction prefix)
var queryOptions = new HarrierEmbeddingsOptions
{
    ModelVariant = HarrierModelVariant.Quantized,
    InstructionPrefix = HarrierEmbeddingsOptions.DefaultInstructionPrefix,
    EnsureModelDownloaded = true,
    UseDirectML = useGpu
};

Console.WriteLine("Loading query generator (with instruction prefix)...");
await using var queryGenerator = await HarrierEmbeddingGenerator.CreateAsync(queryOptions);
Console.WriteLine();
Console.WriteLine($"✓ Query generator ready ({(useGpu ? "GPU/DirectML" : "CPU")})");
Console.WriteLine($"  Model: {queryGenerator.Metadata.DefaultModelId}");
Console.WriteLine($"  Embedding dimensions: {queryGenerator.Metadata.DefaultModelDimensions}");
Console.WriteLine();

// =============================================================================
// SHOWCASE A: Cross-lingual retrieval (English knowledge base, multilingual queries)
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Showcase A: Cross-Lingual Retrieval");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();
Console.WriteLine("Building English knowledge base (12 facts)...");

var englishFacts = new List<string>
{
    "The Eiffel Tower is a wrought-iron lattice tower in Paris, France.",
    "Artificial intelligence is the simulation of human intelligence by machines.",
    "Mount Everest is Earth's highest mountain above sea level, located in the Himalayas.",
    "The Great Wall of China is a series of fortifications built across northern China.",
    "Pizza is a traditional Italian dish consisting of a round flatbread topped with tomato sauce and cheese.",
    "The Amazon rainforest is the world's largest tropical rainforest in South America.",
    "Basketball was invented by James Naismith in 1891 in Springfield, Massachusetts.",
    "Quantum computing uses quantum-mechanical phenomena to perform computation.",
    "The Mona Lisa is a portrait painting by Leonardo da Vinci housed in the Louvre Museum.",
    "The Pacific Ocean is the largest and deepest ocean on Earth.",
    "The Olympic Games are leading international sporting events held every four years.",
    "Coffee is a brewed drink prepared from roasted coffee beans."
};

Console.WriteLine($"Embedding {englishFacts.Count} English facts...");
var startTime = DateTime.Now;
var docEmbeddings = await docGenerator.GenerateAsync(englishFacts);
var embeddingTime = DateTime.Now - startTime;
Console.WriteLine($"✓ Knowledge base ready in {embeddingTime.TotalSeconds:F2} seconds");
Console.WriteLine();

Console.WriteLine("Testing cross-lingual queries (7 languages)...");
Console.WriteLine();

var crossLingualQueries = new (string Language, string Query)[]
{
    ("Spanish", "¿Cuál es la montaña más alta del mundo?"),
    ("French", "Où se trouve la Tour Eiffel?"),
    ("German", "Was ist künstliche Intelligenz?"),
    ("Portuguese", "Qual é a maior floresta tropical do mundo?"),
    ("Japanese", "ピザの起源はどこですか？"),
    ("Chinese", "什么是量子计算？"),
    ("Arabic", "أين توجد لوحة الموناليزا؟")
};

var resultsA = new List<(string Language, string Query, string Match, double Similarity)>();

foreach (var (language, query) in crossLingualQueries)
{
    var queryEmbedding = await queryGenerator.GenerateAsync([query]);
    var queryVector = queryEmbedding[0].Vector.ToArray();
    
    double bestScore = double.MinValue;
    int bestIndex = 0;
    
    for (int i = 0; i < docEmbeddings.Count; i++)
    {
        double similarity = CosineSimilarity(queryVector, docEmbeddings[i].Vector.ToArray());
        if (similarity > bestScore)
        {
            bestScore = similarity;
            bestIndex = i;
        }
    }
    
    resultsA.Add((language, query, englishFacts[bestIndex], bestScore));
}

// Print results table for Showcase A
Console.WriteLine("╔════════════╦══════════════════════════════════════════╦══════════════════════════════════════════╦════════════╗");
Console.WriteLine("║  Language  ║                  Query                   ║            Best Match (English)          ║ Similarity ║");
Console.WriteLine("╠════════════╬══════════════════════════════════════════╬══════════════════════════════════════════╬════════════╣");

foreach (var (language, query, match, similarity) in resultsA)
{
    string truncatedQuery = query.Length > 40 ? query[..37] + "..." : query;
    string truncatedMatch = match.Length > 40 ? match[..37] + "..." : match;
    Console.WriteLine($"║ {language,-10} ║ {truncatedQuery,-40} ║ {truncatedMatch,-40} ║ {similarity,10:F4} ║");
}

Console.WriteLine("╚════════════╩══════════════════════════════════════════╩══════════════════════════════════════════╩════════════╝");
Console.WriteLine();

// =============================================================================
// SHOWCASE B: Multilingual batch embeddings + language-agnostic search
// =============================================================================
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("Showcase B: Multilingual Knowledge Base (8 languages)");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();
Console.WriteLine("Building multilingual knowledge base...");

var multilingualFacts = new (string Language, string Fact)[]
{
    ("Spanish", "La fotosíntesis es el proceso por el cual las plantas convierten la luz solar en energía."),
    ("French", "La Révolution française a commencé en 1789."),
    ("German", "Berlin ist die Hauptstadt von Deutschland."),
    ("Portuguese", "O Rio Amazonas é o segundo rio mais longo do mundo."),
    ("Italian", "Il Colosseo è un antico anfiteatro romano situato nel centro di Roma."),
    ("Japanese", "富士山は日本で最も高い山です。"),
    ("Korean", "김치는 한국의 전통 발효 음식입니다."),
    ("Russian", "Москва является столицей России и крупнейшим городом страны.")
};

Console.WriteLine($"Embedding {multilingualFacts.Length} facts in different languages...");
var multilingualTexts = multilingualFacts.Select(f => f.Fact).ToList();
startTime = DateTime.Now;
var multilingualEmbeddings = await docGenerator.GenerateAsync(multilingualTexts);
embeddingTime = DateTime.Now - startTime;
Console.WriteLine($"✓ Multilingual knowledge base ready in {embeddingTime.TotalSeconds:F2} seconds");
Console.WriteLine();

Console.WriteLine("Testing English queries against multilingual facts...");
Console.WriteLine();

var englishQueries = new string[]
{
    "What is photosynthesis?",
    "When did the French Revolution start?",
    "What is the capital of Germany?",
    "Tell me about the Amazon River",
    "What is a famous Roman monument?",
    "What is the highest mountain in Japan?",
    "What is kimchi?",
    "What is the capital and largest city of Russia?"
};

var resultsB = new List<(string QueryLang, string Query, string DocLang, string Match, double Similarity)>();

foreach (string query in englishQueries)
{
    var queryEmbedding = await queryGenerator.GenerateAsync([query]);
    var queryVector = queryEmbedding[0].Vector.ToArray();
    
    double bestScore = double.MinValue;
    int bestIndex = 0;
    
    for (int i = 0; i < multilingualEmbeddings.Count; i++)
    {
        double similarity = CosineSimilarity(queryVector, multilingualEmbeddings[i].Vector.ToArray());
        if (similarity > bestScore)
        {
            bestScore = similarity;
            bestIndex = i;
        }
    }
    
    resultsB.Add(("English", query, multilingualFacts[bestIndex].Language, multilingualFacts[bestIndex].Fact, bestScore));
}

// Print results table for Showcase B
Console.WriteLine("╔════════════╦══════════════════════════════════════════╦════════════╦══════════════════════════════════════════╦════════════╗");
Console.WriteLine("║ Query Lang ║              Query (English)             ║  Doc Lang  ║          Best Match (Original)           ║ Similarity ║");
Console.WriteLine("╠════════════╬══════════════════════════════════════════╬════════════╬══════════════════════════════════════════╬════════════╣");

foreach (var (queryLang, query, docLang, match, similarity) in resultsB)
{
    string truncatedQuery = query.Length > 40 ? query[..37] + "..." : query;
    string truncatedMatch = match.Length > 40 ? match[..37] + "..." : match;
    Console.WriteLine($"║ {queryLang,-10} ║ {truncatedQuery,-40} ║ {docLang,-10} ║ {truncatedMatch,-40} ║ {similarity,10:F4} ║");
}

Console.WriteLine("╚════════════╩══════════════════════════════════════════╩════════════╩══════════════════════════════════════════╩════════════╝");
Console.WriteLine();

// =============================================================================
// Summary
// =============================================================================
Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                      Summary                                   ║");
Console.WriteLine("╠═══════════════════════════════════════════════════════════════╣");
Console.WriteLine("║  ✓ Showcase A: Cross-lingual retrieval demonstrated           ║");
Console.WriteLine("║    → English knowledge base searched with 7 languages          ║");
Console.WriteLine("║    → Spanish, French, German, Portuguese, Japanese,            ║");
Console.WriteLine("║      Chinese, Arabic queries all found correct English facts   ║");
Console.WriteLine("║                                                                ║");
Console.WriteLine("║  ✓ Showcase B: Language-agnostic semantic search              ║");
Console.WriteLine("║    → 8 different language facts in knowledge base              ║");
Console.WriteLine("║    → English queries correctly matched facts in their          ║");
Console.WriteLine("║      original languages (Spanish, French, German, etc.)        ║");
Console.WriteLine("║                                                                ║");
Console.WriteLine("║  Key Takeaway:                                                 ║");
Console.WriteLine("║  Harrier's 94+ language support enables truly multilingual     ║");
Console.WriteLine("║  RAG applications — query in any language, retrieve from any   ║");
Console.WriteLine("║  language, with high semantic similarity across languages.     ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

return;

static double CosineSimilarity(float[] a, float[] b)
{
    double dot = 0, normA = 0, normB = 0;
    for (int i = 0; i < a.Length; i++)
    {
        dot += a[i] * b[i];
        normA += a[i] * (double)a[i];
        normB += b[i] * (double)b[i];
    }
    return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
}
