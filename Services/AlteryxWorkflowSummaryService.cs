using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Peekerino.Services
{
    internal static class AlteryxWorkflowSummaryService
    {
        private const int MaxToolRows = 12;
        private const int MaxFormulaRows = 50;
        private const int MaxFilterRows = 50;
        private const int MaxExpressionLength = 160;

        internal static AlteryxWorkflowSummary Summarize(string path)
        {
            var doc = XDocument.Load(path, LoadOptions.None);
            var root = doc.Root ?? throw new InvalidDataException("Missing AlteryxDocument root element.");

            string version = root.Attribute("yxmdVer")?.Value
                             ?? root.Attribute("yxmcVer")?.Value
                             ?? root.Attribute("yxpVer")?.Value
                             ?? "Unknown";

            string runE2 = FormatBool(root.Attribute("RunE2")?.Value);

            var toolCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var inputs = new List<string>();
            var outputs = new List<string>();
            var macros = new List<string>();
            int disabledNodes = 0;
            int containerCount = 0;
            int nodeCount = 0;
            var formulaDetails = new List<(string ToolId, string Field, string Expression)>();
            var filterDetails = new List<(string ToolId, string Expression)>();

            XElement? nodesElement = root.Element("Nodes");
            if (nodesElement != null)
            {
                foreach (var node in nodesElement.Descendants("Node"))
                {
                    nodeCount++;
                    string plugin = node.Element("GuiSettings")?.Attribute("Plugin")?.Value ?? "Unknown";
                    string toolId = node.Attribute("ToolID")?.Value ?? "?";
                    Increment(toolCounts, plugin);

                    if (plugin.Contains("ToolContainer", StringComparison.OrdinalIgnoreCase))
                    {
                        containerCount++;
                    }

                    if (IsDisabled(node))
                    {
                        disabledNodes++;
                    }

                    string? macroPath = node.Element("EngineSettings")?.Attribute("Macro")?.Value;
                    if (!string.IsNullOrWhiteSpace(macroPath))
                    {
                        macros.Add(macroPath.Trim());
                    }

                    CollectFileReferences(node, plugin, inputs, outputs);
                    CollectFormulaDetails(node, plugin, toolId, formulaDetails);
                    CollectFilterDetails(node, plugin, toolId, filterDetails);
                }
            }

            XElement? connectionsElement = root.Element("Connections");
            int connectionCount = connectionsElement?.Elements("Connection").Count() ?? 0;

            var summaryBuilder = new StringBuilder();
            summaryBuilder.AppendLine($"Workflow version: {version}");
            summaryBuilder.AppendLine($"Run with E2: {runE2}");
            summaryBuilder.AppendLine();
            summaryBuilder.AppendLine($"Nodes: {nodeCount:N0} ({toolCounts.Count:N0} tool types)");
            summaryBuilder.AppendLine($"Connections: {connectionCount:N0}");
            summaryBuilder.AppendLine($"Containers: {containerCount:N0}");
            if (disabledNodes > 0)
            {
                summaryBuilder.AppendLine($"Disabled nodes: {disabledNodes:N0}");
            }

            AppendList(summaryBuilder, "Input sources:", inputs);
            AppendList(summaryBuilder, "Output targets:", outputs);
            AppendList(summaryBuilder, "Macros referenced:", macros);
            if (formulaDetails.Count > 0)
            {
                summaryBuilder.AppendLine();
                summaryBuilder.AppendLine($"Formula expressions captured: {formulaDetails.Count:N0}");
            }

            if (filterDetails.Count > 0)
            {
                summaryBuilder.AppendLine();
                summaryBuilder.AppendLine($"Filter expressions captured: {filterDetails.Count:N0}");
            }

            var tables = new List<TableSummary>();
            if (toolCounts.Count > 0)
            {
                var sortedTools = toolCounts
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var rows = sortedTools
                    .Select(kv => new[] { kv.Key, kv.Value.ToString(CultureInfo.InvariantCulture) })
                    .ToList();

                tables.Add(new TableSummary("Tool Usage", new[] { "Tool", "Count" }, rows, false));
            }

            if (formulaDetails.Count > 0)
            {
                var distinct = new List<string[]>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in formulaDetails)
                {
                    string key = $"{item.ToolId}|{item.Field}|{item.Expression}";
                    if (seen.Add(key))
                    {
                        distinct.Add(new[]
                        {
                            item.ToolId,
                            string.IsNullOrWhiteSpace(item.Field) ? "(Unnamed)" : item.Field,
                            item.Expression
                        });
                    }
                }

                tables.Add(new TableSummary("Formula Expressions", new[] { "ToolID", "Field", "Expression" }, distinct, false));
            }

            if (filterDetails.Count > 0)
            {
                var distinct = new List<string[]>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in filterDetails)
                {
                    string key = $"{item.ToolId}|{item.Expression}";
                    if (seen.Add(key))
                    {
                        distinct.Add(new[]
                        {
                            item.ToolId,
                            item.Expression
                        });
                    }
                }

                tables.Add(new TableSummary("Filter Expressions", new[] { "ToolID", "Expression" }, distinct, false));
            }

            return new AlteryxWorkflowSummary(summaryBuilder.ToString(), tables);
        }

        private static void CollectFileReferences(XElement node, string plugin, List<string> inputs, List<string> outputs)
        {
            var fileElements = node.Descendants("File");
            foreach (var fileElement in fileElements)
            {
                string value = fileElement.Value?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (IsInputPlugin(plugin))
                {
                    AddIfNotPresent(inputs, value);
                }
                else if (IsOutputPlugin(plugin))
                {
                    AddIfNotPresent(outputs, value);
                }
            }

            if (IsDirectoryPlugin(plugin))
            {
                string directoryPath = node
                    .Descendants("Directory")
                    .Select(x => x.Value?.Trim())
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

                string fileSpec = node
                    .Descendants("FileSpec")
                    .Select(x => x.Value?.Trim())
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    string combined = string.IsNullOrWhiteSpace(fileSpec)
                        ? directoryPath
                        : $"{directoryPath}\\{fileSpec}";
                    AddIfNotPresent(inputs, combined);
                }
            }
        }

        private static void AddIfNotPresent(List<string> list, string value)
        {
            if (!list.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(value);
            }
        }

        private static bool IsInputPlugin(string plugin) =>
            plugin.Contains("Input", StringComparison.OrdinalIgnoreCase);

        private static bool IsOutputPlugin(string plugin) =>
            plugin.Contains("Output", StringComparison.OrdinalIgnoreCase);

        private static bool IsDirectoryPlugin(string plugin) =>
            plugin.Contains("Directory", StringComparison.OrdinalIgnoreCase);

        private static bool IsFormulaPlugin(string plugin) =>
            plugin.Contains("Formula", StringComparison.OrdinalIgnoreCase);

        private static bool IsFilterPlugin(string plugin) =>
            plugin.Contains("Filter", StringComparison.OrdinalIgnoreCase);

        private static void CollectFormulaDetails(XElement node, string plugin, string toolId, List<(string ToolId, string Field, string Expression)> buffer)
        {
            if (!IsFormulaPlugin(plugin))
            {
                return;
            }

            var formulaFields = node
                .Descendants("FormulaField")
                .ToList();

            if (formulaFields.Count == 0)
            {
                return;
            }

            foreach (var formulaField in formulaFields)
            {
                string field = formulaField.Attribute("field")?.Value ?? string.Empty;
                string expression = formulaField.Attribute("expression")?.Value ?? formulaField.Value ?? string.Empty;
                expression = NormalizeExpression(expression);
                if (string.IsNullOrWhiteSpace(expression))
                {
                    continue;
                }

                buffer.Add((toolId, field, expression));
            }
        }

        private static void CollectFilterDetails(XElement node, string plugin, string toolId, List<(string ToolId, string Expression)> buffer)
        {
            if (!IsFilterPlugin(plugin))
            {
                return;
            }

            string? expression = node
                .Descendants("Expression")
                .Select(x => x.Value)
                .FirstOrDefault();

            expression = NormalizeExpression(expression);
            if (string.IsNullOrWhiteSpace(expression))
            {
                return;
            }

            buffer.Add((toolId, expression));
        }

        private static bool IsDisabled(XElement node)
        {
            var disabledElement = node
                .Descendants("Disabled")
                .Attributes("value")
                .FirstOrDefault();

            if (disabledElement == null)
            {
                return false;
            }

            return bool.TryParse(disabledElement.Value, out bool result) && result;
        }

        private static string FormatBool(string? value)
        {
            if (bool.TryParse(value, out bool result))
            {
                return result ? "True" : "False";
            }
            return "Unknown";
        }

        private static void Increment(Dictionary<string, int> counts, string key)
        {
            if (counts.TryGetValue(key, out int value))
            {
                counts[key] = value + 1;
            }
            else
            {
                counts[key] = 1;
            }
        }

        private static string NormalizeExpression(string? expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return string.Empty;
            }

            string normalized = expression
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal);

            normalized = string.Join(" ", normalized
                .Split('\n')
                .Select(part => part.Trim())
                .Where(part => part.Length > 0));

            return normalized;
        }

        private static void AppendList(StringBuilder builder, string header, List<string> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            var distinct = items
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            builder.AppendLine();
            builder.AppendLine(header);
            foreach (var item in distinct)
            {
                builder.AppendLine($"  - {item}");
            }
        }
    }

    internal sealed class AlteryxWorkflowSummary
    {
        public AlteryxWorkflowSummary(string summary, IReadOnlyList<TableSummary> tables)
        {
            Summary = summary;
            Tables = tables;
        }

        public string Summary { get; }
        public IReadOnlyList<TableSummary> Tables { get; }
    }
}

