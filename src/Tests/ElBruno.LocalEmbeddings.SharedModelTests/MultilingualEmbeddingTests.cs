using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings.SharedModelTests;

/// <summary>
/// Shared multilingual and semantic tests that run against every locally available
/// embedding model. Tests use <see cref="SkippableFactAttribute"/> and iterate
/// over all available models, skipping when no models are present.
/// </summary>
public class MultilingualEmbeddingTests
{
    // =========================================================================
    // English
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task English_SimilarSentences_HighSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "The weather is beautiful today",
                "Today has wonderful weather"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity > 0.6, $"[{name}] English similar: expected > 0.6, got {similarity:F4}");
        }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task English_DissimilarSentences_LowSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "The stock market crashed yesterday",
                "My cat loves to sleep on the couch"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity < 0.5, $"[{name}] English dissimilar: expected < 0.5, got {similarity:F4}");
        }
    }

    // =========================================================================
    // Spanish
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task Spanish_SimilarSentences_HighSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "El clima está hermoso hoy",
                "Hoy hace un tiempo maravilloso"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity > 0.5, $"[{name}] Spanish similar: expected > 0.5, got {similarity:F4}");
        }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task Spanish_DissimilarSentences_LowSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "La bolsa de valores cayó ayer",
                "Mi gato adora dormir en el sofá"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity < 0.6, $"[{name}] Spanish dissimilar: expected < 0.6, got {similarity:F4}");
        }
    }

    // =========================================================================
    // Chinese (Simplified)
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task Chinese_SimilarSentences_HighSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "今天天气很好",
                "今天的天气非常棒"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity > 0.5, $"[{name}] Chinese similar: expected > 0.5, got {similarity:F4}");
        }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task Chinese_DissimilarSentences_LowSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "股票市场昨天崩盘了",
                "我的猫喜欢在沙发上睡觉"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity < 0.6, $"[{name}] Chinese dissimilar: expected < 0.6, got {similarity:F4}");
        }
    }

    // =========================================================================
    // French
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task French_SimilarSentences_HighSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "Le temps est magnifique aujourd'hui",
                "Aujourd'hui il fait un temps merveilleux"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity > 0.5, $"[{name}] French similar: expected > 0.5, got {similarity:F4}");
        }
    }

    // =========================================================================
    // German
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task German_SimilarSentences_HighSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "Das Wetter ist heute wunderschön",
                "Heute ist das Wetter herrlich"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity > 0.5, $"[{name}] German similar: expected > 0.5, got {similarity:F4}");
        }
    }

    // =========================================================================
    // Japanese
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task Japanese_SimilarSentences_HighSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "今日はとても良い天気です",
                "今日の天気は素晴らしいです"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity > 0.5, $"[{name}] Japanese similar: expected > 0.5, got {similarity:F4}");
        }
    }

    // =========================================================================
    // Portuguese
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task Portuguese_SimilarSentences_HighSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "O tempo está lindo hoje",
                "Hoje está fazendo um tempo maravilhoso"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity > 0.5, $"[{name}] Portuguese similar: expected > 0.5, got {similarity:F4}");
        }
    }

    // =========================================================================
    // Arabic
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task Arabic_SimilarSentences_HighSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "الطقس جميل اليوم",
                "اليوم الجو رائع جداً"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity > 0.5, $"[{name}] Arabic similar: expected > 0.5, got {similarity:F4}");
        }
    }

    // =========================================================================
    // Korean
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task Korean_SimilarSentences_HighSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "오늘 날씨가 정말 좋습니다",
                "오늘은 날씨가 아주 훌륭합니다"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity > 0.5, $"[{name}] Korean similar: expected > 0.5, got {similarity:F4}");
        }
    }

    // =========================================================================
    // French — Dissimilar
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task French_DissimilarSentences_LowSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "Le temps est magnifique aujourd'hui",
                "La bourse a chuté hier",
                "Mon chat adore dormir sur le canapé"
            ]);

            double similarSim = CosineSimilarity(result[0].Vector, result[0].Vector);
            double dissimilarSim = CosineSimilarity(result[0].Vector, result[2].Vector);
            Assert.True(dissimilarSim < similarSim,
                $"[{name}] French: dissimilar ({dissimilarSim:F4}) should be lower than similar ({similarSim:F4})");
            Assert.True(dissimilarSim < 0.6, $"[{name}] French dissimilar: expected < 0.6, got {dissimilarSim:F4}");
        }
    }

    // =========================================================================
    // German — Dissimilar
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task German_DissimilarSentences_LowSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "Das Wetter ist heute wunderschön",
                "Meine Katze schläft gerne auf dem Sofa"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity < 0.6, $"[{name}] German dissimilar: expected < 0.6, got {similarity:F4}");
        }
    }

    // =========================================================================
    // Japanese — Dissimilar
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task Japanese_DissimilarSentences_LowSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "今日はとても良い天気です",
                "猫はソファで寝るのが好きです"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity < 0.6, $"[{name}] Japanese dissimilar: expected < 0.6, got {similarity:F4}");
        }
    }

    // =========================================================================
    // Portuguese — Dissimilar
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task Portuguese_DissimilarSentences_LowSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "O tempo está lindo hoje",
                "Meu gato adora dormir no sofá"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity < 0.6, $"[{name}] Portuguese dissimilar: expected < 0.6, got {similarity:F4}");
        }
    }

    // =========================================================================
    // Arabic — Dissimilar
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task Arabic_DissimilarSentences_LowSimilarity()
    {
        foreach (var (name, generator) in GetMultilingualGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "الطقس جميل اليوم",
                "قطتي تحب النوم على الأريكة"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity < 0.6, $"[{name}] Arabic dissimilar: expected < 0.6, got {similarity:F4}");
        }
    }

    // =========================================================================
    // Korean — Dissimilar
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task Korean_DissimilarSentences_LowSimilarity()
    {
        foreach (var (name, generator) in GetMultilingualGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "오늘 날씨가 정말 좋습니다",
                "우리 고양이는 소파에서 자는 것을 좋아합니다"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity < 0.6, $"[{name}] Korean dissimilar: expected < 0.6, got {similarity:F4}");
        }
    }

    // =========================================================================
    // Russian
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task Russian_SimilarSentences_HighSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "Сегодня прекрасная погода",
                "Сегодня замечательная погода"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity > 0.5, $"[{name}] Russian similar: expected > 0.5, got {similarity:F4}");
        }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task Russian_DissimilarSentences_LowSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "Сегодня прекрасная погода",
                "Моя кошка любит спать на диване"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity < 0.6, $"[{name}] Russian dissimilar: expected < 0.6, got {similarity:F4}");
        }
    }

    // =========================================================================
    // Cross-lingual: same meaning across languages
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task CrossLingual_EnglishSpanish_SameMeaning_PositiveSimilarity()
    {
        foreach (var (name, generator) in GetMultilingualGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "The weather is beautiful today",
                "El clima está hermoso hoy"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity > 0.3, $"[{name}] Cross-lingual EN-ES: expected > 0.3, got {similarity:F4}");
        }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task CrossLingual_EnglishChinese_SameMeaning_PositiveSimilarity()
    {
        foreach (var (name, generator) in GetMultilingualGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "Machine learning is a subset of artificial intelligence",
                "机器学习是人工智能的一个子集"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity > 0.3, $"[{name}] Cross-lingual EN-ZH: expected > 0.3, got {similarity:F4}");
        }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task CrossLingual_EnglishFrench_SameMeaning_PositiveSimilarity()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync([
                "I love programming",
                "J'adore la programmation"
            ]);

            double similarity = CosineSimilarity(result[0].Vector, result[1].Vector);
            Assert.True(similarity > 0.3, $"[{name}] Cross-lingual EN-FR: expected > 0.3, got {similarity:F4}");
        }
    }

    // =========================================================================
    // Batch: generate embeddings for all 10 languages in one call
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "Multilingual")]
    public async Task Batch_AllLanguages_ProducesValidEmbeddings()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var sentences = new[]
            {
                "The weather is beautiful today",          // English
                "El clima está hermoso hoy",               // Spanish
                "今天天气很好",                              // Chinese
                "Le temps est magnifique aujourd'hui",      // French
                "Das Wetter ist heute wunderschön",         // German
                "今日はとても良い天気です",                    // Japanese
                "O tempo está lindo hoje",                  // Portuguese
                "الطقس جميل اليوم",                         // Arabic
                "오늘 날씨가 정말 좋습니다",                   // Korean
                "Сегодня прекрасная погода"                  // Russian
            };

            var result = await generator.GenerateAsync(sentences);

            Assert.Equal(sentences.Length, result.Count);
            Assert.All(result, e => Assert.True(e.Vector.Length > 0,
                $"[{name}] Embedding should have non-zero dimensions"));

            int expectedDim = result[0].Vector.Length;
            Assert.All(result, e => Assert.Equal(expectedDim, e.Vector.Length));
        }
    }

    // =========================================================================
    // Core embedding properties
    // =========================================================================

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task EmbeddingDimensions_MatchMetadata()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync(["test"]);
            var metadata = generator.GetService<EmbeddingGeneratorMetadata>();
            int? expectedDim = metadata?.DefaultModelDimensions;

            if (expectedDim.HasValue)
            {
                Assert.Equal(expectedDim.Value, result[0].Vector.Length);
            }
            else
            {
                Assert.True(result[0].Vector.Length > 0);
            }
        }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task EmptyInput_ReturnsEmptyResult()
    {
        foreach (var (_, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync(Array.Empty<string>());
            Assert.Empty(result);
        }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task SameInput_ProducesIdenticalEmbeddings()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            const string text = "Deterministic output check";
            var result1 = await generator.GenerateAsync([text]);
            var result2 = await generator.GenerateAsync([text]);

            var v1 = result1[0].Vector.ToArray();
            var v2 = result2[0].Vector.ToArray();
            Assert.Equal(v1.Length, v2.Length);
            for (int i = 0; i < v1.Length; i++)
            {
                Assert.Equal(v1[i], v2[i], 5);
            }
        }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task EmbeddingVector_IsNormalized()
    {
        foreach (var (name, generator) in GetGeneratorsOrSkip())
        {
            var result = await generator.GenerateAsync(["Test normalization of embedding vector"]);
            var vector = result[0].Vector.ToArray();

            double norm = Math.Sqrt(vector.Sum(v => (double)v * v));
            Assert.True(Math.Abs(norm - 1.0) < 0.05,
                $"[{name}] Expected unit vector (norm ≈ 1.0), got {norm:F6}");
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static IReadOnlyList<(string Name, IEmbeddingGenerator<string, Embedding<float>> Generator)> GetGeneratorsOrSkip()
    {
        var generators = ModelFixture.GetAvailableGenerators().ToList();
        Skip.If(generators.Count == 0, "No embedding models available locally — skipping.");
        return generators;
    }

    private static IReadOnlyList<(string Name, IEmbeddingGenerator<string, Embedding<float>> Generator)> GetMultilingualGeneratorsOrSkip()
    {
        var generators = ModelFixture.GetMultilingualGenerators().ToList();
        Skip.If(generators.Count == 0, "No multilingual embedding models available locally — skipping. Install Harrier to run these tests.");
        return generators;
    }

    private static double CosineSimilarity(ReadOnlyMemory<float> a, ReadOnlyMemory<float> b)
    {
        var spanA = a.Span;
        var spanB = b.Span;
        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < spanA.Length; i++)
        {
            dot += spanA[i] * (double)spanB[i];
            normA += spanA[i] * (double)spanA[i];
            normB += spanB[i] * (double)spanB[i];
        }
        double denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denominator == 0 ? 0 : dot / denominator;
    }
}
