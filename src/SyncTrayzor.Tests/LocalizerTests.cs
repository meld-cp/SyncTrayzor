using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using SyncTrayzor.Localization;
using Xunit;

namespace SyncTrayzor.Tests
{
    /// <summary>
    /// DummyArgument is a class that can be implicitly converted to various types required by SmartFormat strings.
    /// </summary>
    public class DummyArgument : IEnumerable<object>
    {
        private readonly List<object> _items = new() { "dummy" };

        public override string ToString() => "dummy";

        public static implicit operator int(DummyArgument _) => 1;
        public static implicit operator string(DummyArgument d) => d.ToString();

        public IEnumerator<object> GetEnumerator() => _items.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class LocalizerTests
    {
        private static readonly object[] DummyArgs =
            Enumerable.Repeat<object>(new DummyArgument(), 10).ToArray();

        [Fact]
        public void FormatWithCulture_AllResourceStringsAreValidSmartFormatStrings()
        {
            var testAssembly = Assembly.GetExecutingAssembly();
            var testDir = Path.GetDirectoryName(testAssembly.Location);
            var resourcesDir = Path.Combine(testDir, "Resources");
            var resxFiles = Directory.GetFiles(resourcesDir, "Resources*.resx");

            foreach (var resxFile in resxFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(resxFile);
                var culture = CultureInfo.GetCultureInfo("en-US");
                if (fileName.Contains('.'))
                {
                    var cultureName = fileName[(fileName.LastIndexOf('.') + 1)..];
                    culture = CultureInfo.GetCultureInfo(cultureName);
                }

                var doc = XDocument.Load(resxFile);
                var dataElements = doc.Descendants("data");

                foreach (var data in dataElements)
                {
                    var name = data.Attribute("name")?.Value;
                    var value = data.Element("value")?.Value;

                    if (string.IsNullOrEmpty(value))
                        continue;

                    try
                    {
                        Localizer.FormatWithCulture(culture, value, DummyArgs);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(
                            $"Invalid SmartFormat string in {Path.GetFileName(resxFile)}, key '{name}': {ex.Message}",
                            ex);
                    }
                }
            }
        }
    }
}