namespace AstraVaultVectorGen;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: astra-vault-vector-gen <test-vectors/av3-directory>");
            return 2;
        }

        var root = Path.GetFullPath(args[0]);
        var inputPath = Path.Combine(root, "reference-input.json");
        var outputDir = Path.Combine(root, "reference-output");
        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"Missing {inputPath}");
            return 2;
        }

        if (!outputDir.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Output path escape blocked.");
            return 2;
        }

        var input = ReferenceInputDocument.Load(inputPath);
        if (input.VectorSchemaVersion != 1)
        {
            Console.Error.WriteLine("Unsupported vector_schema_version.");
            return 2;
        }

        var writer = new GoldenVectorWriter(input, outputDir);
        writer.GenerateAll();
        Console.WriteLine($"Golden vectors written to {outputDir}");
        return 0;
    }
}