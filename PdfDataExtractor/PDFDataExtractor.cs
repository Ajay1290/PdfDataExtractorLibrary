using System;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text.Json;

namespace PdfDataExtractor
{
    public class PDFDataExtractor
    {
        private string _pass;
        public string _path;

        private RegexOptions options = RegexOptions.Multiline;

        public PDFDataExtractor(string path, string pass)
        {
            _path = path;
            _pass = pass;
        }

        private string readPDF()
        {
            byte[] ownerPassword = Encoding.ASCII.GetBytes(this._pass);
            PdfReader reader = new PdfReader(this._path, ownerPassword: ownerPassword);
            SimpleTextExtractionStrategy strategy = new SimpleTextExtractionStrategy();

            string rawText = "";
            for (int z = 1; z <= reader.NumberOfPages; z++)
            {
                string s = getRawText(reader, z, strategy).ToString();
                File.WriteAllText(@"./temp.txt", s);
                rawText += s;
            };

            // Did this to resolve text duplication problem of ITextSharp Lib.
            rawText = File.ReadAllText(@"./temp.txt");

            return rawText;
        }


        public string extract()
        {
            string rawText = readPDF();
            JsonObject jsonObj = new JsonObject();
            jsonObj.Client_details = getPersonalData(rawText, this.options);
            jsonObj.Funds = new List<Fund>();
            jsonObj.Funds = getFundsData(rawText, this.options);

            //// This is what we want !!!
            string jsonOutput = JsonSerializer.Serialize(jsonObj);

            File.WriteAllText(@"./CAS_output.json", jsonOutput);

            return jsonOutput;
        }



        private Client getPersonalData(string rawText, RegexOptions options)
        {
            // Email: 1, Name: 2, Mobile: 4 only for page 1
            string pattern = @"Email Id:.(.*)\n(.*)(.*\n)*Mobile: ([0-9]{10})";

            Client client = new Client();

            foreach (Match m in Regex.Matches(rawText, pattern, options))
            {
                client.Email = $"{m.Groups[1]}";
                client.Name = $"{m.Groups[2]}";
                client.Mobile = $"{m.Groups[4]}";
            }

            return client;
        }

        private List<Fund> getFundsData(string rawText, RegexOptions options)
        {
            // AMC: 4, Fund: 8, Pan: 5, Folio: 14, Date: 16, Amount: 18, Price: 21, Units: 24, Desc: 26, Balance: 29
            string pattern = @"((((.*Mutual Fund$)\n?)(PAN:.([A-Z]{5}[0-9]{4}[A-Z]))?)(.*\n)?(.*)([(\[]Advisor:))?((.*\n)?)?((Folio No:\s(\d*\s/\s\d*))(.*\n)*?)?([0-9]{1,2}-[A-Z|a-z]{1,3}-[0-9]{1,4})\s([(\[]?((\d*,?)*\d+\.\d*)[)\[]?)\s([(\[]?((\d*,?)*\d+\.\d*)[)\[]?)\s([(\[]?((\d*,?)*\d+\.\d*)[)\[]?)\s(.*)(\s|\s-\s)([(\[]?((\d*,?)*\d+\.\d*)[)\[]?)(.*\n)*?";

            string valuePattern = @"(Valuation\son\s[0-9]{1,2}-[A-Z|a-z]{1,3}-[0-9]{1,4}:\sINR\s([(\[]?[0-9]{1,},?[0-9]{1,}.[0-9]{1,})[)\[]?)(.*\n)*?Closing\sUnit\sBalance:\s[(\[]?([0-9]{1,},?[0-9]{1,}.[0-9]{1,})[)\[]?";

            List<Fund> funds = new List<Fund>();

            List<Dictionary<string, string>> values = new List<Dictionary<string, string>>();

            foreach (Match m in Regex.Matches(rawText, valuePattern, options))
            {
                Dictionary<string, string> ubItem = new Dictionary<string, string>();
                ubItem.Add("Total_Balance", $"{m.Groups[2]}");
                ubItem.Add("Total_Units", $"{m.Groups[4]}");
                values.Add(ubItem);
            }

            int j = -1; // did intentionally
            foreach (Match m in Regex.Matches(rawText, pattern, options))
            {
                if (m.Groups[14].ToString() != "")
                {
                    j += 1;
                    Fund fund = new Fund();
                    fund.AMC_Name = $"{m.Groups[4]}";
                    fund.Fund_Name = $"{m.Groups[8]}";
                    fund.Folio_Number = $"{m.Groups[14]}";
                    try
                    {
                        fund.Total_Balance = $"{values[j]["Total_Balance"]}";
                        fund.Total_Units = $"{values[j]["Total_Units"]}";
                    }
                    catch (Exception e)
                    {
                        Console.Write($"{e}");
                    }
                    IList<Transaction> transactions = new List<Transaction>();
                    fund.Transactions = transactions;
                    funds.Add(fund);
                }
            }

            int i = -1; // did intentionally
            foreach (Match m in Regex.Matches(rawText, pattern, options))
            {
                if (m.Groups[14].ToString() == "")
                {
                    funds[i] = getFundData(m, funds[i]);
                }
                else
                {
                    i += 1;
                    getFundData(m, funds[i], fresh: true);
                }
            }
            return funds;
        }

        private Fund getFundData(Match m, Fund fund = null, Boolean fresh = false)
        {
            Transaction transaction = new Transaction();
            transaction.Date = $"{m.Groups[16]}";
            transaction.Description = $"{m.Groups[26]}";
            transaction.Amount = $"{m.Groups[18]}";
            transaction.Units = $"{m.Groups[24]}";
            transaction.Price = $"{m.Groups[21]}";
            transaction.Balance_Units = $"{m.Groups[29]}";

            fund.Transactions.Add(transaction);

            return fund;
        }


        //public static string getFolioData(string rawText, RegexOptions options) {
        //    // Folio: 1
        //    string pattern = @"Folio No:.([0-9]{7}./.[0-9]{2})";
        //    string folioNo = "";
        //    foreach (Match m in Regex.Matches(rawText, pattern, options)) {
        //        folioNo = $"{m.Groups[1]}";
        //        //Console.WriteLine($"Folio No: {m.Groups[1]} \n");
        //    }

        //    return folioNo;
        //}

        //public static IList<Transaction> getTransactionsData(string rawText, RegexOptions options){
        //    string pattern = @"((Folio No:\s(\d*\s/\s\d*))(.*\n)*?)?([0-9]{1,2}-[A-Z|a-z]{1,3}-[0-9]{1,4})\s([(\[]?((\d*,?)*\d+\.\d*)[)\[]?)\s([(\[]?((\d*,?)*\d+\.\d*)[)\[]?)\s([(\[]?((\d*,?)*\d+\.\d*)[)\[]?)\s(.*[A-Z|a-z|0-9])(\s|\s-\s)([(\[]?((\d*,?)*\d+\.\d*)[)\[]?)";

        //    IList<Transaction> transactions = new List<Transaction>();

        //    foreach (Match m in Regex.Matches(rawText, pattern, options)) {
        //        Transaction transaction = new Transaction();
        //        transaction.Date = $"{m.Groups[1]}";
        //        transaction.Description = $"{m.Groups[11]}";
        //        transaction.Amount = $"{m.Groups[3]}";
        //        transaction.Units = $"{m.Groups[9]}";
        //        transaction.Price = $"{m.Groups[5]}";
        //        transaction.Balance_Units   = $"{m.Groups[14]}";
        //        Console.WriteLine(
        //            $"Date: {m.Groups[1]} \n" +
        //            $"Amount: {m.Groups[3]} \n" +
        //            $"Price: {m.Groups[5]} \n" +
        //            $"Units: {m.Groups[9]} \n" +
        //            $"Description: {m.Groups[11]} \n" +
        //            $"Unit Balance: {m.Groups[14]} \n"
        //        );
        //        transactions.Add(transaction);
        //    }

        //    return transactions;
        //}

        private static string getRawText(PdfReader reader, int page, SimpleTextExtractionStrategy strategy)
        {
            return PdfTextExtractor.GetTextFromPage(reader, page, strategy).ToString();
        }
    }

    public class JsonObject
    {
        public Client Client_details { get; set; }
        public IList<Fund> Funds { get; set; }
    }

    public class Client
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Mobile { get; set; }
    }

    public class Transaction
    {
        public string Date { get; set; }
        public string Description { get; set; }
        public string Amount { get; set; }
        public string Units { get; set; }
        public string Price { get; set; }
        public string Balance_Units { get; set; }
    }

    public class Fund
    {
        public string AMC_Name { get; set; }
        public string Folio_Number { get; set; }
        public string Fund_Name { get; set; }
        public string Total_Units { get; set; }
        public string Total_Balance { get; set; }
        public IList<Transaction> Transactions { get; set; }
    }
}
