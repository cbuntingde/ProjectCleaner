namespace ProjectCleaner.Models;

/// <summary>
/// The built-in catalogue of things that get left behind by installing, building and running apps.
/// Anything with <c>enabledByDefault: false</c> is opt-in because the name is generic enough to
/// collide with real source.
/// </summary>
public static class PatternCatalog
{
    public const string NodeCategory = "Node / JavaScript";
    public const string PythonCategory = "Python";
    public const string DotNetCategory = ".NET / C#";
    public const string JvmCategory = "Java / JVM";
    public const string PhpCategory = "PHP";
    public const string RustCategory = "Rust / Go";
    public const string IdeCategory = "IDE / editor";
    public const string BuildOutputCategory = "Build output";
    public const string OsCruftCategory = "OS cruft";

    public static IReadOnlyList<CleanupPattern> CreateAll() =>
    [
        // ---- Node / JavaScript -------------------------------------------------
        F(NodeCategory, "node_modules", "npm/yarn/pnpm install output. Safe to delete; reinstalled by <npm install>."),
        F(NodeCategory, ".next", "Next.js build cache and output."),
        F(NodeCategory, ".nuxt", "Nuxt build cache and output."),
        F(NodeCategory, ".svelte-kit", "SvelteKit generated output."),
        F(NodeCategory, ".parcel-cache", "Parcel bundler cache."),
        F(NodeCategory, ".turbo", "Turborepo task cache."),
        F(NodeCategory, ".angular", "Angular CLI build cache."),
        F(NodeCategory, ".cache", "Generic tool cache folder (webpack, gulp, storybook, etc.)."),
        F(NodeCategory, ".output", "Vite / SvelteKit / Nuxt prerendered output."),
        Fi(NodeCategory, ".eslintcache", "ESLint incremental cache file."),
        Fi(NodeCategory, "npm-debug.log", "npm crash log."),
        Fi(NodeCategory, "yarn-error.log", "Yarn crash log."),

        // ---- Python ------------------------------------------------------------
        F(PythonCategory, "__pycache__", "Compiled bytecode cache. Regenerated on next import."),
        F(PythonCategory, ".pytest_cache", "pytest last-failed / node-id cache."),
        F(PythonCategory, ".mypy_cache", "mypy incremental type cache."),
        F(PythonCategory, ".ruff_cache", "Ruff linter cache."),
        F(PythonCategory, ".tox", "tox virtualenv + work directories."),
        F(PythonCategory, ".nox", "nox session directories."),
        F(PythonCategory, ".ipynb_checkpoints", "Jupyter autosave checkpoints."),
        F(PythonCategory, ".venv", "Python virtual environment. Recreated by <python -m venv>."),
        F(PythonCategory, "venv", "Python virtual environment (older convention). Recreated by <python -m venv>."),
        F(PythonCategory, "*.egg-info", "setuptools package metadata."),
        Fi(PythonCategory, "*.pyc", "Compiled Python bytecode file."),
        Fi(PythonCategory, "*.pyo", "Optimised Python bytecode file."),

        // ---- .NET / C# ---------------------------------------------------------
        F(DotNetCategory, "bin", "MSBuild output folder. Recreated by <dotnet build>."),
        F(DotNetCategory, "obj", "MSBuild intermediate folder (assets, generated code). Recreated by <dotnet build>."),
        F(DotNetCategory, ".vs", "Visual Studio solution cache and user state."),
        F(DotNetCategory, "TestResults", "Test run output (TRX, coverage, logs)."),
        F(DotNetCategory, "packages", "Legacy NuGet restore folder.", enabledByDefault: false),
        Fi(DotNetCategory, "*.user", "Visual Studio per-user settings file."),

        // ---- Java / JVM --------------------------------------------------------
        F(JvmCategory, ".gradle", "Gradle caches and build state."),
        F(JvmCategory, ".m2", "Maven local repository (every installed artifact).", enabledByDefault: false),
        F(JvmCategory, "build", "Gradle / Maven build output.", enabledByDefault: false),

        // ---- PHP ---------------------------------------------------------------
        F(PhpCategory, "vendor", "Composer install output. Recreated by <composer install>."),

        // ---- Rust / Go ---------------------------------------------------------
        F(RustCategory, "target", "Cargo build output and incremental cache."),
        F(RustCategory, ".gocache", "Go build cache."),

        // ---- IDE / editor ------------------------------------------------------
        F(IdeCategory, ".fleet", "JetBrains Fleet local state/cache."),
        F(IdeCategory, ".history", "VS Code local history folder."),

        // ---- Build output (generic names - opt in) -----------------------------
        F(BuildOutputCategory, "dist", "Distribution / bundle output.", enabledByDefault: false),
        F(BuildOutputCategory, "out", "Generic compiler output folder.", enabledByDefault: false),
        F(BuildOutputCategory, "coverage", "Test coverage report output."),
        F(BuildOutputCategory, ".nyc_output", "Istanbul raw coverage data."),
        F(BuildOutputCategory, ".sass-cache", "Sass compiler cache."),
        F(BuildOutputCategory, "DerivedData", "Xcode derived data."),
        F(BuildOutputCategory, ".terraform", "Terraform provider/plugin cache and state.", enabledByDefault: false),

        // ---- OS cruft ----------------------------------------------------------
        Fi(OsCruftCategory, ".DS_Store", "macOS folder metadata."),
        Fi(OsCruftCategory, "Thumbs.db", "Windows Explorer thumbnail cache."),
        Fi(OsCruftCategory, "desktop.ini", "Folder customisation file.", enabledByDefault: false),
        Fi(OsCruftCategory, "*.tmp", "Temporary file."),
        Fi(OsCruftCategory, "*.log", "Log file.", enabledByDefault: false),
        Fi(OsCruftCategory, "*.bak", "Backup file.", enabledByDefault: false),
        Fi(OsCruftCategory, "*.old", "Rotated / superseded file.", enabledByDefault: false),
    ];

    private static CleanupPattern F(string category, string match, string description, bool enabledByDefault = true)
        => new(category, match, PatternKind.Folder, description, enabledByDefault);

    private static CleanupPattern Fi(string category, string match, string description, bool enabledByDefault = true)
        => new(category, match, PatternKind.File, description, enabledByDefault);
}