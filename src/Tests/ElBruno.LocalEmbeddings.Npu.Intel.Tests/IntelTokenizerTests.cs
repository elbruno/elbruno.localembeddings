namespace ElBruno.LocalEmbeddings.Npu.Intel.Tests;

public class IntelTokenizerTests
{
    [Fact]
    public void Tokenize_RemovesAccentsForUncasedBertVocabulary()
    {
        var vocabPath = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(vocabPath, ["[PAD]", "[UNK]", "[CLS]", "[SEP]", "[MASK]", "cafe", "sao", "naive", "resume"]);
            var tokenizer = new IntelTokenizer(vocabPath, maxLength: 8);

            var (inputIds, attentionMask) = tokenizer.Tokenize("Café São naïve résumé");

            Assert.Equal([2L, 5L, 6L, 7L, 8L, 3L, 0L, 0L], inputIds);
            Assert.Equal([1L, 1L, 1L, 1L, 1L, 1L, 0L, 0L], attentionMask);
        }
        finally
        {
            File.Delete(vocabPath);
        }
    }
}
