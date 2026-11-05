using Nethereum.Testing;

namespace Nethereum.EEST.ConformanceRunner
{
    public static class ConformanceSettings
    {
        public static string? Case => TestEnvironment.SettingOrNull("Conformance", "Case");

        public static bool Diagnose => !bool.TryParse(TestEnvironment.SettingOrNull("Conformance", "Diagnose"), out var on) || on;

        public static int DiagnoseMax => int.TryParse(TestEnvironment.SettingOrNull("Conformance", "DiagnoseMax"), out var max) ? max : 10;
    }
}
