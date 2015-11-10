﻿using System;
using System.Data.Services.Design;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using EnvDTE;
using Microsoft.OData.ConnectedService.Common;
using Microsoft.VisualStudio.ConnectedServices;

namespace Microsoft.OData.ConnectedService.CodeGeneration
{
    internal class V3CodeGenDescriptor : BaseCodeGenDescriptor
    {
        public V3CodeGenDescriptor(string metadataUri, ConnectedServiceHandlerContext context, Project project)
            : base(metadataUri, context, project)
        {
            this.ClientNuGetPackageName = Common.Constants.V3ClientNuGetPackage;
            this.ClientDocUri = Common.Constants.V3DocUri;
        }

        public async override Task AddNugetPackages()
        {
            await this.Context.Logger.WriteMessageAsync(LoggerMessageCategory.Information, "Adding Nuget Packages");

            var wcfDSInstallLocation = CodeGeneratorUtils.GetWCFDSInstallLocation();
            var packageSource = Path.Combine(wcfDSInstallLocation, @"bin\NuGet");
            if (Directory.Exists(packageSource))
            {
                var files = Directory.EnumerateFiles(packageSource, "*.nupkg").ToList();
                foreach (var nugetPackage in Common.Constants.V3NuGetPackages)
                {
                    if (!files.Any(f => Regex.IsMatch(f, nugetPackage + @"(.\d){2,4}.nupkg")))
                    {
                        packageSource = Common.Constants.NuGetOnlineRepository;
                    }
                }
            }
            else
            {
                packageSource = Common.Constants.NuGetOnlineRepository;
            }

            if (!PackageInstallerServices.IsPackageInstalled(this.Project, this.ClientNuGetPackageName))
            {
                Version packageVersion = null;
                PackageInstaller.InstallPackage(Common.Constants.NuGetOnlineRepository, this.Project, this.ClientNuGetPackageName, packageVersion, false);
            }
        }

        public async override Task AddGeneratedClientCode()
        {
            await this.Context.Logger.WriteMessageAsync(LoggerMessageCategory.Information, "Generating Client Proxy ...");

            ODataConnectedServiceInstance codeGenInstance = (ODataConnectedServiceInstance)this.Context.ServiceInstance;

            EntityClassGenerator generator = new EntityClassGenerator(LanguageOption.GenerateCSharpCode);
            generator.UseDataServiceCollection = codeGenInstance.UseDataServiceCollection;
            generator.Version = DataServiceCodeVersion.V3;

            XmlReaderSettings settings = new XmlReaderSettings()
            {
                XmlResolver = new XmlUrlResolver()
                {
                    Credentials = System.Net.CredentialCache.DefaultNetworkCredentials
                }
            };

            using (XmlReader reader = XmlReader.Create(this.MetadataUri, settings))
            {
                string tempFile = Path.GetTempFileName();

                using (StreamWriter writer = File.CreateText(tempFile))
                {
                    var errors = generator.GenerateCode(reader, writer, codeGenInstance.NamespacePrefix);
                    if (errors != null && errors.Count() > 0)
                    {
                        foreach (var err in errors)
                        {
                            await this.Context.Logger.WriteMessageAsync(LoggerMessageCategory.Warning, err.Message);
                        }
                    }
                }

                string outputFile = GetReferenceFilePath();
                await this.Context.HandlerHelper.AddFileAsync(tempFile, outputFile);
            }
        }
    }
}