using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace Peekerino.Services
{
    internal static class IncaDocumentSummarizer
    {
        private static readonly XmlReaderSettings ReaderSettings = new()
        {
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
            DtdProcessing = DtdProcessing.Ignore
        };

        private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
        private static readonly XNamespace IncaData = "http://schemas.itello.se/Inca/datamodel";

        public static bool TrySummarize(string path, CancellationToken ct, out string summary)
        {
            summary = string.Empty;
            try
            {
                using var preflight = XmlReader.Create(path, ReaderSettings);
                if (preflight.MoveToContent() != XmlNodeType.Element)
                {
                    return false;
                }

                var isInca = string.Equals(preflight.LocalName, "incaDocument", StringComparison.OrdinalIgnoreCase) &&
                             preflight.NamespaceURI.Contains("schemas.itello.se/Inca", StringComparison.OrdinalIgnoreCase);

                if (!isInca)
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

            try
            {
                using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = XmlReader.Create(fileStream, ReaderSettings);
                var document = XDocument.Load(reader);
                summary = BuildSummary(document, ct);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                summary = $"Error summarizing INCA document: {ex.Message}";
                return true;
            }
        }

        private static string BuildSummary(XDocument document, CancellationToken ct)
        {
            var sb = new StringBuilder();
            var root = document.Root;
            if (root == null)
            {
                return "Empty INCA document.";
            }

            var iddm = root.Name.Namespace;

            AppendDocumentHeader(sb, root);
            sb.AppendLine();

            var addressee = root.Element(iddm + "addressee");
            if (addressee != null)
            {
                AppendAddressee(sb, addressee, iddm);
                sb.AppendLine();
            }

            var companies = root.Elements(iddm + "administratingCompany").ToList();
            if (companies.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                AppendCompanies(sb, companies, iddm);
                sb.AppendLine();
            }

            var insurances = root.Elements(iddm + "insurance").ToList();
            for (int i = 0; i < insurances.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                AppendInsurance(sb, insurances[i], iddm, i + 1);
                sb.AppendLine();
            }

            var benefits = root.Elements(iddm + "benefit").ToList();
            if (benefits.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                AppendBenefits(sb, benefits, iddm, string.Empty);
                sb.AppendLine();
            }

            var reserves = root.Descendants(IncaData + "valueReserve")
                               .Take(5)
                               .ToList();
            if (reserves.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                AppendValueReserves(sb, reserves);
                sb.AppendLine();
            }

            var persons = root.Elements(iddm + "person").ToList();
            if (persons.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                AppendPersons(sb, persons, iddm);
            }

            return sb.ToString().TrimEnd();
        }

        private static void AppendDocumentHeader(StringBuilder sb, XElement root)
        {
            var parts = new List<string>();
            AddPart(parts, "type", root.Attribute(Xsi + "type")?.Value);
            AddPart(parts, "printDocumentId", root.Attribute("printDocumentId")?.Value);
            AddPart(parts, "incaVersion", root.Attribute("incaVersion")?.Value);
            AddPart(parts, "printDate", root.Attribute("printDate")?.Value);
            AddPart(parts, "userName", root.Attribute("userName")?.Value);

            sb.AppendLine($"Document: {root.Name}");
            if (parts.Count > 0)
            {
                sb.AppendLine("  " + string.Join(" | ", parts));
            }
        }

        private static void AppendAddressee(StringBuilder sb, XElement addressee, XNamespace iddm)
        {
            var type = addressee.Attribute(Xsi + "type")?.Value ?? addressee.Name.LocalName;
            var personId = addressee.Attribute("personId")?.Value;
            sb.AppendLine($"Addressee ({type}{FormatId(personId)})");

            var firstName = addressee.Element(iddm + "firstName")?.Value;
            var name = addressee.Element(iddm + "name")?.Value;
            if (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(name))
            {
                sb.AppendLine($"  Name: {string.Join(" ", new[] { firstName, name }.Where(s => !string.IsNullOrWhiteSpace(s)))}");
            }

            var language = addressee.Element(iddm + "language")?.Attribute("language")?.Value;
            var status = addressee.Element(iddm + "status")?.Value;
            var extras = new List<string>();
            AddPart(extras, "Language", language);
            AddPart(extras, "Status", status);
            if (extras.Count > 0)
            {
                sb.AppendLine("  " + string.Join(" | ", extras));
            }

            var address = addressee.Element(iddm + "address");
            if (address != null)
            {
                var lines = new[]
                {
                    address.Element(iddm + "addressRow1")?.Value,
                    address.Element(iddm + "row1")?.Value,
                    address.Element(iddm + "row2")?.Value,
                    address.Element(iddm + "row3")?.Value,
                    address.Element(iddm + "city")?.Value,
                    address.Element(iddm + "postCode")?.Value,
                    address.Element(iddm + "country")?.Attribute("countryCode")?.Value
                }.Where(s => !string.IsNullOrWhiteSpace(s));

                var addressSummary = string.Join(", ", lines);
                if (!string.IsNullOrWhiteSpace(addressSummary))
                {
                    sb.AppendLine($"  Address: {addressSummary}");
                }
            }
        }

        private static void AppendCompanies(StringBuilder sb, IReadOnlyCollection<XElement> companies, XNamespace iddm)
        {
            if (companies.Count == 0)
            {
                return;
            }

            sb.AppendLine($"Administrating Company ({companies.Count})");
            foreach (var company in companies)
            {
                var parts = new List<string>
                {
                    $"id={company.Attribute("administratingCompanyId")?.Value ?? "?"}"
                };
                AddPart(parts, "Name", company.Element(iddm + "administratingCompanyName")?.Value);
                AddPart(parts, "Country", company.Element(iddm + "operationsCountry")?.Attribute("countryCode")?.Value);
                AddPart(parts, "LegalPerson", company.Element(iddm + "legalPerson")?.Attribute("personId")?.Value);
                sb.AppendLine("  " + string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p))));
            }
        }

        private static void AppendInsurance(StringBuilder sb, XElement insurance, XNamespace iddm, int index)
        {
            var headerParts = new List<string>
            {
                $"contractId={insurance.Attribute("contractId")?.Value ?? "?"}"
            };
            AddPart(headerParts, "status", insurance.Element(iddm + "contractStatus")?.Value);
            AddPart(headerParts, "subtype", insurance.Element(iddm + "contractSubtype")?.Value);
            AddPart(headerParts, "start", insurance.Element(iddm + "contractStartDate")?.Value);

            sb.AppendLine($"Insurance #{index} ({string.Join(", ", headerParts.Where(p => !string.IsNullOrWhiteSpace(p)))})");

            var currency = insurance.Element(iddm + "contractCurrency")?.Attribute("currencyCode")?.Value;
            var managementType = insurance.Element(iddm + "investmentManagementType")?.Value;
            var extras = new List<string>();
            AddPart(extras, "Currency", currency);
            AddPart(extras, "Management", managementType);
            if (extras.Count > 0)
            {
                sb.AppendLine("  " + string.Join(" | ", extras));
            }

            var product = insurance.Element(iddm + "product");
            if (product != null)
            {
                var productParts = new List<string>();
                AddPart(productParts, "Name", product.Element(iddm + "variantName")?.Value ?? product.Element(iddm + "variantDescription")?.Value);
                AddPart(productParts, "Short", product.Element(iddm + "variantShortName")?.Value);
                AddPart(productParts, "VariantId", product.Attribute("variantId")?.Value);
                if (productParts.Count > 0)
                {
                    sb.AppendLine("  Product: " + string.Join(" | ", productParts));
                }
            }

            var roles = insurance.Elements(iddm + "contractRole").ToList();
            if (roles.Count > 0)
            {
                sb.AppendLine("  Roles:");
                var table = new TextTableBuilder(new[] { "Role", "PersonId", "From", "To" });
                foreach (var role in roles)
                {
                    var roleName = role.Element(iddm + "role")?.Value ?? "(unknown role)";
                    var personId = role.Element(iddm + "person")?.Attribute("personId")?.Value ?? string.Empty;
                    var from = role.Attribute("contractRoleFrom")?.Value ?? string.Empty;
                    var to = role.Attribute("contractRoleTo")?.Value ?? string.Empty;
                    table.AddRow(new[] { roleName, personId, from, to });
                }

                foreach (var line in table.Build().Split(Environment.NewLine))
                {
                    sb.AppendLine("  " + line);
                }
            }

            var benefits = insurance.Elements(iddm + "benefit").ToList();
            if (benefits.Count > 0)
            {
                AppendBenefits(sb, benefits, iddm, indent: "  ");
            }
        }

        private static void AppendBenefits(StringBuilder sb, IReadOnlyCollection<XElement> benefits, XNamespace iddm, string indent)
        {
            var prefix = string.IsNullOrEmpty(indent) ? string.Empty : indent;
            sb.AppendLine($"{prefix}Benefits ({benefits.Count})");
            var primaryTable = new TextTableBuilder(new[]
            {
                "ContractId", "Status", "Type", "Start", "Fee"
            });

            var paymentTable = new TextTableBuilder(new[]
            {
                "ContractId", "Frequency", "From", "To", "Months", "%", "Receiver", "Type", "End"
            });

            var beneficiaryTable = new TextTableBuilder(new[]
            {
                "ContractId", "Beneficiary", "From", "To", "Flags"
            });

            var compensationDetails = new List<string[]>();

            foreach (var benefit in benefits.Take(6))
            {
                var parts = new List<string>
                {
                    $"contractId={benefit.Attribute("contractId")?.Value ?? "?"}"
                };
                AddPart(parts, "status", benefit.Element(iddm + "contractStatus")?.Value);
                AddPart(parts, "type", benefit.Element(iddm + "benefitType")?.Value);
                AddPart(parts, "currency", benefit.Element(iddm + "contractCurrency")?.Attribute("currencyCode")?.Value);
                AddPart(parts, "start", benefit.Element(iddm + "contractStartDate")?.Value);
                AddPart(parts, "feeTechnique", benefit.Element(iddm + "feeTechnique")?.Value);
                primaryTable.AddRow(new[]
                {
                    benefit.Attribute("contractId")?.Value ?? string.Empty,
                    benefit.Element(iddm + "contractStatus")?.Value ?? string.Empty,
                    benefit.Element(iddm + "benefitType")?.Value ?? string.Empty,
                    benefit.Element(iddm + "contractStartDate")?.Value ?? string.Empty,
                    benefit.Element(iddm + "feeTechnique")?.Value ?? string.Empty
                });

                var beneficiary = benefit.Element(iddm + "beneficiary");
                if (beneficiary != null)
                {
                    var beneficiaryParts = new List<string>();
                    AddPart(beneficiaryParts, "id", beneficiary.Element(iddm + "beneficiary")?.Value);
                    AddPart(beneficiaryParts, "from", beneficiary.Attribute("beneficiaryFrom")?.Value);
                    AddPart(beneficiaryParts, "to", beneficiary.Attribute("beneficiaryTo")?.Value);

                    var flagValues = new[]
                    {
                        ("cancelable", beneficiary.Element(iddm + "cancelable")?.Value),
                        ("disposition", beneficiary.Element(iddm + "disposition")?.Value),
                        ("privateProperty", beneficiary.Element(iddm + "privateProperty")?.Value)
                    }.Where(tuple => !string.IsNullOrWhiteSpace(tuple.Item2))
                     .Select(tuple => $"{tuple.Item1}={tuple.Item2}")
                     .ToList();

                    var beneficiaryLine = string.Join(" | ", beneficiaryParts.Where(p => !string.IsNullOrWhiteSpace(p)));
                    if (!string.IsNullOrWhiteSpace(beneficiaryLine) || flagValues.Count > 0)
                    {
                        var suffix = string.Join(", ", flagValues);
                        beneficiaryTable.AddRow(new[]
                        {
                            benefit.Attribute("contractId")?.Value ?? string.Empty,
                            beneficiary.Element(iddm + "beneficiary")?.Value ?? string.Empty,
                            beneficiary.Attribute("beneficiaryFrom")?.Value ?? string.Empty,
                            beneficiary.Attribute("beneficiaryTo")?.Value ?? string.Empty,
                            suffix
                        });
                    }
                }

                var payment = benefit.Element(iddm + "benefitAmount");
                if (payment != null)
                {
                    var paymentParts = new List<string>();
                    AddPart(paymentParts, "frequency", benefit.Element(iddm + "outPaymentFrequency")?.Value ?? payment.Element(iddm + "outPaymentFrequency")?.Value);
                    AddPart(paymentParts, "from", payment.Element(iddm + "benefitAmountPaymentFromDate")?.Value);
                    AddPart(paymentParts, "to", payment.Element(iddm + "benefitAmountPaymentToDate")?.Value);
                    AddPart(paymentParts, "months", payment.Element(iddm + "benefitAmountPaymentTimeInMonths")?.Value);
                    AddPart(paymentParts, "percent", payment.Element(iddm + "benefitAmountPercentage")?.Value);
                    paymentTable.AddRow(new[]
                    {
                        benefit.Attribute("contractId")?.Value ?? string.Empty,
                        benefit.Element(iddm + "outPaymentFrequency")?.Value ?? payment.Element(iddm + "outPaymentFrequency")?.Value ?? string.Empty,
                        payment.Element(iddm + "benefitAmountPaymentFromDate")?.Value ?? string.Empty,
                        payment.Element(iddm + "benefitAmountPaymentToDate")?.Value ?? string.Empty,
                        payment.Element(iddm + "benefitAmountPaymentTimeInMonths")?.Value ?? string.Empty,
                        payment.Element(iddm + "benefitAmountPercentage")?.Value ?? string.Empty,
                        payment.Element(iddm + "benefitAmountPaymentReceiver")?.Value ?? string.Empty,
                        payment.Element(iddm + "benefitAmountPaymentType")?.Value ?? string.Empty,
                        payment.Element(iddm + "benefitEndDate")?.Value ?? string.Empty
                    });
                }

                var compensation = benefit.Element(iddm + "contractCompensationAllocation");
                if (compensation != null)
                {
                    var compParts = new List<string>();
                    AddPart(compParts, "from", compensation.Attribute("contractCompensationAllocationFrom")?.Value);
                    AddPart(compParts, "to", compensation.Attribute("contractCompensationAllocationTo")?.Value);
                    var compDetails = compensation.Elements(iddm + "contractCompensationAllocationDetail")
                                                  .Select(d =>
                                                  {
                                                      var share = d.Element(iddm + "contractCompensationAllocationPercentage")?.Value ?? "?";
                                                      var person = d.Element(iddm + "person")?.Attribute("personId")?.Value ?? "?";
                                                      return $"{share}→{person}";
                                                  }).ToList();
                    if (compDetails.Count > 0)
                    {
                        compParts.Add("detail: " + string.Join(", ", compDetails));
                    }

                    compensationDetails.Add(new[]
                    {
                        benefit.Attribute("contractId")?.Value ?? string.Empty,
                        compensation.Attribute("contractCompensationAllocationFrom")?.Value ?? string.Empty,
                        compensation.Attribute("contractCompensationAllocationTo")?.Value ?? string.Empty,
                        compDetails.Count > 0 ? string.Join(", ", compDetails) : string.Empty
                    });
                }
            }

            foreach (var line in primaryTable.Build().Split(Environment.NewLine))
            {
                sb.AppendLine(prefix + "  " + line);
            }

            if (beneficiaryTable.Build() is { Length: > 0 } beneficiaryTableText)
            {
                sb.AppendLine(prefix + "  Beneficiaries:");
                foreach (var line in beneficiaryTableText.Split(Environment.NewLine))
                {
                    sb.AppendLine(prefix + "  " + line);
                }
            }

            if (paymentTable.Build() is { Length: > 0 } paymentTableText)
            {
                sb.AppendLine(prefix + "  Payments:");
                foreach (var line in paymentTableText.Split(Environment.NewLine))
                {
                    sb.AppendLine(prefix + "  " + line);
                }
            }

            if (compensationDetails.Count > 0)
            {
                var compTable = new TextTableBuilder(new[] { "ContractId", "From", "To", "Details" });
                foreach (var row in compensationDetails)
                {
                    compTable.AddRow(row);
                }

                sb.AppendLine(prefix + "  Compensation:");
                foreach (var line in compTable.Build().Split(Environment.NewLine))
                {
                    sb.AppendLine(prefix + "  " + line);
                }

                sb.AppendLine();
            }

            if (benefits.Count > 6)
            {
                sb.AppendLine($"{prefix}  ... {benefits.Count - 6:N0} more benefit node(s)");
            }
        }

        private static void AppendValueReserves(StringBuilder sb, IReadOnlyCollection<XElement> reserves)
        {
            sb.AppendLine($"Value Reserves (showing {reserves.Count})");
            foreach (var reserve in reserves)
            {
                var parts = new List<string>
                {
                    $"id={reserve.Attribute("valueReserveId")?.Value ?? "?"}"
                };
                AddPart(parts, "amount", reserve.Element(IncaData + "reserveTotalAmount")?.Value);
                AddPart(parts, "calculatedTo", reserve.Element(IncaData + "reserveCalculatedToDate")?.Value);
                AddPart(parts, "subtype", reserve.Element(IncaData + "valueReserveSubtype")?.Value);
                sb.AppendLine("  " + string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p))));
            }
        }

        private static void AppendPersons(StringBuilder sb, IReadOnlyCollection<XElement> persons, XNamespace iddm)
        {
            sb.AppendLine($"Persons ({persons.Count})");
            foreach (var person in persons.Take(6))
            {
                var parts = new List<string>
                {
                    $"id={person.Attribute("personId")?.Value ?? "?"}"
                };
                AddPart(parts, "type", person.Attribute(Xsi + "type")?.Value ?? person.Name.LocalName);
                AddPart(parts, "name", person.Element(iddm + "name")?.Value ?? person.Element(iddm + "firstName")?.Value);
                AddPart(parts, "status", person.Element(iddm + "status")?.Value);
                AddPart(parts, "DOB", person.Element(iddm + "dateOfBirth")?.Value);
                AddPart(parts, "DOD", person.Element(iddm + "dateOfDeath")?.Value);
                sb.AppendLine("  " + string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p))));
            }

            if (persons.Count > 6)
            {
                sb.AppendLine($"  ... {persons.Count - 6:N0} more person node(s)");
            }
        }

        private static void AddPart(ICollection<string> parts, string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{label}: {value}");
            }
        }

        private static string FormatId(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : $" (personId={value})";
        }

        private static string FormatRange(string? from, string? to)
        {
            if (string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(from))
            {
                return $"→ {to}";
            }

            if (string.IsNullOrWhiteSpace(to))
            {
                return $"{from} →";
            }

            return $"{from} → {to}";
        }
    }
}

