using System.Xml.Linq;
using Game.Application;
using Game.Domain;
using Game.Godot;

namespace Game.Tests;

public sealed class ArchitectureTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Application_ui_contract_does_not_expose_mutable_state_or_concrete_services()
    {
        AssertCleanApplicationContract(typeof(IMainGameApplication));
        Assert.Throws<InvalidOperationException>(() => AssertCleanApplicationContract(typeof(IViolatingContract)));
    }

    [Fact]
    public void Presenters_depend_only_on_application_contract()
    {
        foreach (var type in new[] { typeof(MainStrategicPresenter), typeof(MainBattlePresenter), typeof(MainSaveController) })
            AssertPresenterContract(type);
        Assert.Throws<InvalidOperationException>(() => AssertPresenterContract(typeof(ViolatingPresenter)));
    }

    [Fact]
    public void Domain_and_application_project_dependencies_keep_their_direction()
    {
        AssertProjectDependencies("src/Game.Domain/Game.Domain.csproj");
        AssertProjectDependencies("src/Game.Application/Game.Application.csproj");
        var violating = XDocument.Parse("<Project><ItemGroup><ProjectReference Include=\"..\\Game.Infrastructure\\Game.Infrastructure.csproj\" /></ItemGroup></Project>");
        Assert.Throws<InvalidOperationException>(() => AssertProjectDependencies(violating));
        Assert.Throws<InvalidOperationException>(() => AssertNoConcreteStorage("class C { void M() { File.WriteAllText(\"x\", \"y\"); } }"));
    }

    [Fact]
    public void Godot_views_and_presenters_do_not_use_mutable_state_rules_or_infrastructure()
    {
        var compositionAndVerificationExceptions = new HashSet<string>(StringComparer.Ordinal) { "Main.cs", "StrategicMap.cs" };
        foreach (var path in Directory.GetFiles(Path.Combine(Root, "src/Game.Godot"), "*.cs"))
        {
            if (compositionAndVerificationExceptions.Contains(Path.GetFileName(path))) continue;
            var source = File.ReadAllText(path);
            Assert.Empty(FindForbiddenGodotDependencies(source));
        }
        Assert.NotEmpty(FindForbiddenGodotDependencies("class V { void M() { MainBattleService.Resolve(null); } }"));
        Assert.NotEmpty(FindForbiddenGodotDependencies("using Game.Infrastructure;"));
        Assert.NotEmpty(FindForbiddenGodotDependencies("class V { MainWorld State { get; } }"));
    }

    private static void AssertCleanApplicationContract(Type contract)
    {
        foreach (var method in contract.GetMethods())
        {
            AssertCleanType(method.ReturnType);
            foreach (var parameter in method.GetParameters()) AssertCleanType(parameter.ParameterType);
        }
    }

    private static void AssertCleanType(Type type)
    {
        if (type == typeof(MainWorld) || type == typeof(MainContent) || type == typeof(MainGameSession) ||
            type == typeof(MainBattleState) || type == typeof(MainCity) || type == typeof(MainPerson) || type == typeof(MainPeriod) ||
            type.Namespace == "Game.Infrastructure")
            throw new InvalidOperationException($"应用合同暴露可变状态或具体服务：{type.FullName}");
        if (type.IsArray) AssertCleanType(type.GetElementType()!);
        if (type.IsGenericType)
            foreach (var argument in type.GetGenericArguments()) AssertCleanType(argument);
    }

    private static void AssertPresenterContract(Type presenter)
    {
        var constructors = presenter.GetConstructors();
        if (constructors.Length != 1 || constructors[0].GetParameters() is not [{ ParameterType: var parameter }] || parameter != typeof(IMainGameApplication))
            throw new InvalidOperationException($"Presenter 必须只依赖应用合同：{presenter.FullName}");
    }

    private static void AssertProjectDependencies(string relativePath) => AssertProjectDependencies(XDocument.Load(Path.Combine(Root, relativePath)));

    private static void AssertProjectDependencies(XDocument project)
    {
        foreach (var reference in project.Descendants("ProjectReference").Select(element => element.Attribute("Include")?.Value ?? ""))
        {
            if (reference.Contains("Game.Godot", StringComparison.Ordinal) || reference.Contains("Game.Infrastructure", StringComparison.Ordinal))
                throw new InvalidOperationException($"Domain/Application 出现非法工程依赖：{reference}");
        }
        foreach (var source in new[] { "src/Game.Domain", "src/Game.Application" }.SelectMany(path => Directory.GetFiles(Path.Combine(Root, path), "*.cs")))
            AssertNoConcreteStorage(File.ReadAllText(source));
    }

    private static void AssertNoConcreteStorage(string source)
    {
        if (source.Contains("using System.IO", StringComparison.Ordinal) || source.Contains("File.", StringComparison.Ordinal) ||
            source.Contains("Directory.", StringComparison.Ordinal))
            throw new InvalidOperationException("Domain/Application 包含具体文件存储。");
    }

    private static IReadOnlyList<string> FindForbiddenGodotDependencies(string source)
    {
        var forbidden = new[]
        {
            "using Game.Infrastructure", "MainBattleService", "MainFactionRules", "TurnCoordinator", "MainContentLoader",
            "MainSaveService", "MainSaveCatalog", "session.World", "MainWorld", "MainContent", "MainPeriod "
        };
        return forbidden.Where(source.Contains).ToList();
    }

    private interface IViolatingContract
    {
        MainWorld GetWorld();
    }

    private sealed class ViolatingPresenter
    {
        public ViolatingPresenter(MainGameSession session) { }
    }
}
