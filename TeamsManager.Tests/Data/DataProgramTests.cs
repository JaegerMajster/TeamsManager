using System;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace TeamsManager.Tests.Data
{
    /// <summary>
    /// Podstawowe testy dla Program.cs z projektu TeamsManager.Data
    /// Testuje czy aplikacja konsolowa jest poprawnie skonfigurowana
    /// </summary>
    public class DataProgramTests
    {
        [Fact]
        public void Program_Class_ShouldExist()
        {
            // Arrange & Act
            var dataAssembly = Assembly.LoadFrom(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "TeamsManager.Data.dll"));

            // Assert
            var programType = dataAssembly.GetType("TeamsManager.Data.Program");
            programType.Should().NotBeNull("klasa Program powinna istnieć w TeamsManager.Data");
        }

        [Fact]
        public void Program_ShouldHaveMainMethod()
        {
            // Arrange
            var dataAssembly = Assembly.LoadFrom(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "TeamsManager.Data.dll"));
            var programType = dataAssembly.GetType("TeamsManager.Data.Program");

            // Act
            var mainMethod = programType?.GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            // Assert
            mainMethod.Should().NotBeNull("Program powinien mieć metodę Main");
            mainMethod!.IsStatic.Should().BeTrue("metoda Main powinna być statyczna");
        }

        [Fact]
        public void Program_MainMethod_ShouldAcceptStringArrayParameter()
        {
            // Arrange
            var dataAssembly = Assembly.LoadFrom(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "TeamsManager.Data.dll"));
            var programType = dataAssembly.GetType("TeamsManager.Data.Program");
            var mainMethod = programType?.GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            // Act
            var parameters = mainMethod?.GetParameters();

            // Assert
            parameters.Should().NotBeNull();
            parameters!.Length.Should().Be(1, "Main powinien przyjmować jeden parametr");
            parameters[0].ParameterType.Should().Be(typeof(string[]), "parametr powinien być typu string[]");
        }

        [Fact]
        public void Program_MainMethod_ShouldReturnTask()
        {
            // Arrange
            var dataAssembly = Assembly.LoadFrom(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "TeamsManager.Data.dll"));
            var programType = dataAssembly.GetType("TeamsManager.Data.Program");
            var mainMethod = programType?.GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            // Act & Assert
            mainMethod?.ReturnType.Should().Be(typeof(System.Threading.Tasks.Task), 
                "Main powinien zwracać Task (async Main)");
        }

        [Fact]
        public void DataAssembly_ShouldReferenceEntityFrameworkCore()
        {
            // Arrange
            var dataAssembly = Assembly.LoadFrom(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "TeamsManager.Data.dll"));

            // Act
            var referencedAssemblies = dataAssembly.GetReferencedAssemblies();

            // Assert
            referencedAssemblies.Should().Contain(a => a.Name == "Microsoft.EntityFrameworkCore",
                "TeamsManager.Data powinien referencować Entity Framework Core");
        }

        [Fact]
        public void DataAssembly_ShouldReferenceTeamsManagerCore()
        {
            // Arrange
            var dataAssembly = Assembly.LoadFrom(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "TeamsManager.Data.dll"));

            // Act
            var referencedAssemblies = dataAssembly.GetReferencedAssemblies();

            // Assert
            referencedAssemblies.Should().Contain(a => a.Name == "TeamsManager.Core",
                "TeamsManager.Data powinien referencować TeamsManager.Core");
        }

        [Fact]
        public void DataAssembly_ShouldHaveCorrectTargetFramework()
        {
            // Arrange
            var dataAssembly = Assembly.LoadFrom(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "TeamsManager.Data.dll"));

            // Act
            var targetFrameworkAttribute = dataAssembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();

            // Assert
            targetFrameworkAttribute.Should().NotBeNull("assembly powinien mieć TargetFrameworkAttribute");
            targetFrameworkAttribute!.FrameworkName.Should().StartWith(".NETCoreApp,Version=v9.0",
                "projekt powinien targetować .NET 9.0");
        }

        [Fact]
        public void Program_Namespace_ShouldBeCorrect()
        {
            // Arrange
            var dataAssembly = Assembly.LoadFrom(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "TeamsManager.Data.dll"));

            // Act
            var programType = dataAssembly.GetType("TeamsManager.Data.Program");

            // Assert
            programType.Should().NotBeNull();
            programType!.Namespace.Should().Be("TeamsManager.Data", 
                "Program powinien być w namespace TeamsManager.Data");
        }

        [Theory]
        [InlineData("TeamsManager.Data.TeamsManagerDbContext")]
        [InlineData("TeamsManager.Data.TestDataSeeder")]
        [InlineData("TeamsManager.Data.DesignTimeDbContextFactory")]
        public void DataAssembly_ShouldContainExpectedTypes(string expectedTypeName)
        {
            // Arrange
            var dataAssembly = Assembly.LoadFrom(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "TeamsManager.Data.dll"));

            // Act
            var type = dataAssembly.GetType(expectedTypeName);

            // Assert
            type.Should().NotBeNull($"assembly powinien zawierać typ {expectedTypeName}");
        }

        [Fact]
        public void Program_ShouldBeInternalClass()
        {
            // Arrange
            var dataAssembly = Assembly.LoadFrom(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "TeamsManager.Data.dll"));

            // Act
            var programType = dataAssembly.GetType("TeamsManager.Data.Program");

            // Assert
            programType.Should().NotBeNull();
            programType!.IsPublic.Should().BeFalse("Program powinien być klasą wewnętrzną");
            programType.IsNotPublic.Should().BeTrue("Program nie powinien być publiczny");
        }

        [Fact]
        public void DataProject_ShouldCompileSuccessfully()
        {
            // Arrange & Act
            var dataAssemblyPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "TeamsManager.Data.dll");

            // Assert
            File.Exists(dataAssemblyPath).Should().BeTrue(
                "TeamsManager.Data.dll powinien istnieć po kompilacji");

            var assembly = Assembly.LoadFrom(dataAssemblyPath);
            assembly.Should().NotBeNull("assembly powinien się załadować bez błędów");
        }

        [Fact]
        public void DataProject_ShouldHaveCorrectEntryPoint()
        {
            // Arrange
            var dataAssembly = Assembly.LoadFrom(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "TeamsManager.Data.dll"));

            // Act
            var entryPoint = dataAssembly.EntryPoint;

            // Assert
            entryPoint.Should().NotBeNull("projekt powinien mieć entry point");
            entryPoint!.Name.Should().Be("<Main>", "entry point powinien być metodą <Main> (async Main)");
            entryPoint.DeclaringType!.Name.Should().Be("Program", "Main powinien być w klasie Program");
        }
    }
} 