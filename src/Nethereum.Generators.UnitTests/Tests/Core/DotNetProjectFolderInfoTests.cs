using Nethereum.Generators.Core;
using Nethereum.Generators.Tests.Common;
using System.IO;
using Xunit;

namespace Nethereum.Generators.UnitTests.Tests.Core
{
    public class DotNetProjectFolderInfoTests
    {
        [Fact]
        public void CanReadRootNamespaceFromProjectFile()
        {
            var context = new ProjectTestContext(this.GetType().Name, "Test01");
            try
            {
                context.CreateProject();
                context.SetRootNamespaceInProject("Test");

                var folderInfo = new DotNetProjectFolderInfo(context.TargetProjectFolder);

                Assert.Equal("Test", folderInfo.RootNamespace);
            }
            finally
            {
                context.CleanUp();
            }
        }

        [Fact]
        public void CanReadAssemblyNameFromProjectFile()
        {
            var context = new ProjectTestContext(this.GetType().Name, "Test02");
            try
            {
                context.CreateProject();
                context.SetAssemblyNameInProject("TestAssembly");

                var folderInfo = new DotNetProjectFolderInfo(context.TargetProjectFolder);

                Assert.Equal("TestAssembly", folderInfo.AssemblyName);
                Assert.Equal("TestAssembly", folderInfo.RootNamespace);
            }
            finally
            {
                context.CleanUp();
            }
        }

        [Fact]
        public void RootNamespaceTakesPreferenceToAssemblyName()
        {
            var context = new ProjectTestContext(this.GetType().Name, "Test03");
            try
            {
                context.CreateProject();
                context.SetRootNamespaceInProject("TestNamespace");
                context.SetAssemblyNameInProject("TestAssembly");

                var folderInfo = new DotNetProjectFolderInfo(context.TargetProjectFolder);

                Assert.Equal("TestNamespace", folderInfo.RootNamespace);
            }
            finally
            {
                context.CleanUp();
            }
        }

        [Fact]
        public void ReadsProjectInformationFromProjectFilePath()
        {
            var context = new ProjectTestContext(this.GetType().Name, "Test04");
            try
            {
                context.CreateProject();

                var folderInfo = new DotNetProjectFolderInfo(context.ProjectFilePath);

                AssertDefaultProjectValues(context, folderInfo);
            }
            finally
            {
                context.CleanUp();
            }
        }

        [Fact]
        public void ReadsProjectInformationFromProjectFolderPath()
        {
            var context = new ProjectTestContext(this.GetType().Name, "Test05");
            try
            {
                context.CreateProject();

                var folderInfo = new DotNetProjectFolderInfo(context.TargetProjectFolder);

                AssertDefaultProjectValues(context, folderInfo);
            }
            finally
            {
                context.CleanUp();
            }
        }

        private static void AssertDefaultProjectValues(ProjectTestContext context, DotNetProjectFolderInfo folderInfo)
        {
            Assert.Equal(context.ProjectName, folderInfo.RootNamespace);
            Assert.Equal(context.ProjectName, folderInfo.AssemblyName);
            Assert.Equal(context.ProjectFilePath, folderInfo.FullPathToProjectFile);
            Assert.Equal(context.TargetProjectFolder, folderInfo.FullPathToProjectFolder);
            Assert.Equal(Path.GetFileName(context.ProjectFilePath), folderInfo.ProjectFileName);
        }
    }
}
