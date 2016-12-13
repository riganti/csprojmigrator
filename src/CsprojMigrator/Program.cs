using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("Usage: CsprojMigrator.exe <path to csproj file>");
            Console.WriteLine();
            Environment.Exit(1);
        }

        // load XML
        var xml = XDocument.Load(args[0]);

        // update the format to RC Refresh
        RemoveXmlns(xml);
        SetProjectSdk(xml);
        RemoveImportMicrosoftCommonProps(xml);
        RemoveWildcardIncludes(xml);
        ConvertPackageReferenceVersionsToAttributes(xml);
        UpgradeToMsbuild2Final(xml);
        RemoveImportMicrosoftCsharpTargets(xml);

        // save
        File.Copy(args[0], args[0] + ".bak");
        using (var fs = File.OpenWrite(args[0]))
        {
            xml.Save(fs);
        }
    }

    private static void RemoveXmlns(XDocument xml)
    {
        // removes the default xmlns
        foreach (var element in xml.Root.DescendantsAndSelf())
        {
            element.Name = XName.Get(element.Name.LocalName);
            element.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
        }
    }

    private static void SetProjectSdk(XDocument xml)
    {
        // determine whether we are referencing Web SDK or a normal one
        var webSdk = xml.Descendants("PackageReference")
            .FirstOrDefault(n => string.Equals(n.Attribute("Include")?.Value, "Microsoft.NET.Sdk.Web", StringComparison.CurrentCultureIgnoreCase));

        // set the Project Sdk version
        if (webSdk == null)
        {
            var normalSdk = xml.Descendants("PackageReference")
                .FirstOrDefault(n => string.Equals(n.Attribute("Include")?.Value, "Microsoft.NET.Sdk", StringComparison.CurrentCultureIgnoreCase));

            if (normalSdk == null)
            {
                Console.WriteLine("INFO: No Sdk PackageReference was found. Microsoft.NET.Sdk will be used.");
            }
            xml.Root.SetAttributeValue("Sdk", "Microsoft.NET.Sdk");
            normalSdk?.Remove();
        }
        else
        {
            Console.WriteLine("INFO: Microsoft.NET.Sdk.Web will be used.");
            xml.Root.SetAttributeValue("Sdk", "Microsoft.NET.Sdk.Web");
            webSdk.Remove();
        }
    }

    private static void RemoveImportMicrosoftCommonProps(XDocument xml)
    {
        var imports = xml.Root.Elements("Import")
            .Where(e => string.Equals(e.Attribute("Project")?.Value, @"$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props", StringComparison.CurrentCultureIgnoreCase));

        if (imports.Count() == 0)
        {
            Console.WriteLine("WARNING: Import of Microsoft.Common.props was not found.");
        }
        imports.Remove();
    }

    private static void RemoveWildcardIncludes(XDocument xml)
    {
        // remove wildcard *.cs includes
        var compileInclude = xml.Descendants("Compile")
            .Where(c => c.Attribute("Include")?.Value == @"**\*.cs");
        if (compileInclude.Count() == 0)
        {
            Console.WriteLine("WARNING: Wildcard include of *.cs files was not found.");
        }
        compileInclude.Remove();

        // remove wildcard *.resx includes
        var embeddedResourceInclude = xml.Descendants("EmbeddedResource")
            .Where(c => c.Attribute("Include")?.Value == @"**\*.resx");
        if (embeddedResourceInclude.Count() == 0)
        {
            Console.WriteLine("WARNING: Wildcard include of *.resx files was not found.");
        }
        embeddedResourceInclude.Remove();

        // remove empty ItemGroups
        xml.Elements("ItemGroup")
            .Where(e => !e.HasElements)
            .Remove();
    }

    private static void ConvertPackageReferenceVersionsToAttributes(XDocument xml)
    {
        var references = xml.Descendants()
            .Where(e => e.Name == "PackageReference" || e.Name == "DotNetCliToolReference");
        foreach (var reference in references)
        {
            var version = reference.Element("Version");
            if (version != null)
            {
                // convert inner element to attribute
                reference.SetAttributeValue("Version", version.Value);
                version.Remove();
            }
            else if (reference.Attribute("Version") == null)
            {
                // there is neither the Version element nor the attribute
                Console.WriteLine($"WARNING: The {reference.Name.LocalName} to {reference.Attribute("Include")?.Value} doesn't contain the Version element.");
            }
        }
    }

    private static void UpgradeToMsbuild2Final(XDocument xml)
    {
        var references = xml.Descendants()
            .Where(e => e.Name == "PackageReference" || e.Name == "DotNetCliToolReference")
            .Where(e => e.Attribute("Version")?.Value == "1.0.0-msbuild1-final");
        foreach (var reference in references)
        {
            reference.SetAttributeValue("Version", "1.0.0-msbuild2-final");
        }
    }

    private static void RemoveImportMicrosoftCsharpTargets(XDocument xml)
    {
        var imports = xml.Root.Elements("Import")
            .Where(e => string.Equals(e.Attribute("Project")?.Value, @"$(MSBuildToolsPath)\Microsoft.CSharp.targets", StringComparison.CurrentCultureIgnoreCase));

        if (imports.Count() == 0)
        {
            Console.WriteLine("WARNING: Import of Microsoft.Csharp.targets was not found.");
        }
        imports.Remove();
    }
}