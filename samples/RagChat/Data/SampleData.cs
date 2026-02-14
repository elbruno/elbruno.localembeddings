using RagChat.VectorStore;

namespace RagChat.Data;

/// <summary>
/// Provides sample FAQ data for the RAG demo.
/// </summary>
public static class SampleData
{
    /// <summary>
    /// Gets a collection of sample FAQ documents about a fictional AI assistant product.
    /// </summary>
    public static List<Document> GetFaqDocuments() =>
    [
        // Getting Started
        new Document
        {
            Id = "faq-001",
            Title = "What is LocalAI Assistant?",
            Content = "LocalAI Assistant is an AI-powered productivity tool that runs entirely on your local machine. It provides intelligent document analysis, code assistance, and natural language processing without sending data to external servers. Your data stays private and secure.",
            Category = "Getting Started"
        },
        new Document
        {
            Id = "faq-002",
            Title = "System Requirements",
            Content = "LocalAI Assistant requires Windows 10/11, macOS 12+, or Ubuntu 20.04+. Minimum hardware: 8GB RAM, 4-core CPU. Recommended: 16GB RAM, 8-core CPU with AVX2 support. GPU acceleration is optional but improves performance significantly with NVIDIA CUDA or Apple Silicon.",
            Category = "Getting Started"
        },
        new Document
        {
            Id = "faq-003",
            Title = "How to Install",
            Content = "To install LocalAI Assistant, download the installer from our website, run the setup wizard, and follow the prompts. On Windows, you may need to install the Visual C++ Redistributable. On macOS, drag the app to Applications folder. On Linux, use the provided .deb or .rpm package.",
            Category = "Getting Started"
        },
        new Document
        {
            Id = "faq-004",
            Title = "First Time Setup",
            Content = "After installation, launch LocalAI Assistant and complete the initial setup wizard. You'll choose your preferred AI model (small, medium, or large), set your default language, and configure privacy settings. The setup takes about 5 minutes and downloads necessary model files.",
            Category = "Getting Started"
        },

        // Features
        new Document
        {
            Id = "faq-005",
            Title = "Document Analysis Feature",
            Content = "The document analysis feature lets you upload PDFs, Word documents, or text files for AI-powered analysis. It can summarize content, extract key points, answer questions about the document, and identify important entities like names, dates, and organizations.",
            Category = "Features"
        },
        new Document
        {
            Id = "faq-006",
            Title = "Code Assistant Feature",
            Content = "The code assistant helps developers write, review, and debug code. It supports 50+ programming languages including Python, JavaScript, C#, Java, and Go. Features include code completion, bug detection, refactoring suggestions, and documentation generation.",
            Category = "Features"
        },
        new Document
        {
            Id = "faq-007",
            Title = "Semantic Search",
            Content = "Semantic search allows you to find information using natural language queries instead of exact keywords. Ask questions like 'What are our Q3 sales figures?' and the system finds relevant documents even if they don't contain those exact words. Powered by local embedding models.",
            Category = "Features"
        },
        new Document
        {
            Id = "faq-008",
            Title = "Chat Interface",
            Content = "The chat interface provides a conversational way to interact with your documents and data. You can ask follow-up questions, request clarifications, and have multi-turn conversations. Chat history is saved locally and can be exported or deleted at any time.",
            Category = "Features"
        },

        // Privacy & Security
        new Document
        {
            Id = "faq-009",
            Title = "Data Privacy",
            Content = "LocalAI Assistant processes all data locally on your machine. No documents, queries, or personal information are sent to external servers. The AI models run entirely offline after initial download. You maintain complete control over your data.",
            Category = "Privacy & Security"
        },
        new Document
        {
            Id = "faq-010",
            Title = "Encryption and Storage",
            Content = "All local data is encrypted using AES-256 encryption. The application stores data in a secure local database with configurable location. You can enable additional security features like password protection and automatic data purging after inactivity.",
            Category = "Privacy & Security"
        },
        new Document
        {
            Id = "faq-011",
            Title = "Enterprise Security Features",
            Content = "Enterprise edition includes SSO integration, audit logging, role-based access control, and compliance certifications including SOC2 and HIPAA. IT administrators can deploy via group policy and manage settings centrally.",
            Category = "Privacy & Security"
        },

        // Troubleshooting
        new Document
        {
            Id = "faq-012",
            Title = "Slow Performance",
            Content = "If LocalAI Assistant is running slowly, try these solutions: Close other memory-intensive applications, reduce the AI model size in settings, enable GPU acceleration if available, or increase the allocated memory in advanced settings. Restarting the application can also help.",
            Category = "Troubleshooting"
        },
        new Document
        {
            Id = "faq-013",
            Title = "Model Loading Errors",
            Content = "Model loading errors usually occur due to corrupted downloads or insufficient disk space. To fix: Check you have at least 5GB free disk space, delete the model cache folder and re-download, verify file integrity using the built-in verification tool, or try a smaller model.",
            Category = "Troubleshooting"
        },
        new Document
        {
            Id = "faq-014",
            Title = "Application Crashes",
            Content = "If the application crashes frequently, check for updates first. Common fixes include: updating graphics drivers, running as administrator on Windows, checking system memory availability, and reviewing crash logs in the settings menu. Contact support with logs if issues persist.",
            Category = "Troubleshooting"
        },
        new Document
        {
            Id = "faq-015",
            Title = "Connection Issues",
            Content = "While LocalAI Assistant runs offline, it needs internet for initial setup, updates, and model downloads. If you see connection errors: check firewall settings, ensure proxy is configured if required, try disabling VPN temporarily, and verify the download server status on our status page.",
            Category = "Troubleshooting"
        },

        // Pricing & Licensing
        new Document
        {
            Id = "faq-016",
            Title = "Pricing Plans",
            Content = "LocalAI Assistant offers three tiers: Free (basic features, small model), Professional ($9.99/month, all features, medium model), and Enterprise (custom pricing, advanced security, large models, priority support). All plans include unlimited local processing.",
            Category = "Pricing & Licensing"
        },
        new Document
        {
            Id = "faq-017",
            Title = "License Activation",
            Content = "To activate your license, go to Settings > License and enter your license key. For offline activation, use the manual activation option which generates a request code. Licenses are per-user and can be transferred to a new machine once per year.",
            Category = "Pricing & Licensing"
        },
        new Document
        {
            Id = "faq-018",
            Title = "Refund Policy",
            Content = "We offer a 30-day money-back guarantee for all paid plans. To request a refund, contact support with your order number. Refunds are processed within 5-7 business days. Note that enterprise contracts have separate terms specified in the agreement.",
            Category = "Pricing & Licensing"
        },

        // Integration
        new Document
        {
            Id = "faq-019",
            Title = "API Integration",
            Content = "LocalAI Assistant provides a REST API for integration with other applications. The API runs on localhost and supports document upload, text analysis, embedding generation, and chat completions. Full API documentation is available in the developer portal.",
            Category = "Integration"
        },
        new Document
        {
            Id = "faq-020",
            Title = "IDE Plugins",
            Content = "Official plugins are available for Visual Studio Code, Visual Studio, JetBrains IDEs (IntelliJ, PyCharm, Rider), and Neovim. Plugins provide inline code assistance, documentation lookup, and quick actions without leaving your development environment.",
            Category = "Integration"
        }
    ];
}
