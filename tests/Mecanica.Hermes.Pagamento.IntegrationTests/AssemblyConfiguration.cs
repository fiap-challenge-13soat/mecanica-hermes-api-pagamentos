using Xunit;

// Integration tests compartilham containers Testcontainers e env vars globais — paralelismo
// entre fixtures causaria race condition. Forçamos execução sequencial para manter
// determinismo (mesmo padrão do Cadastros/OS).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
