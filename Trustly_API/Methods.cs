using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Trustly_API.Models;

namespace Trustly_API
{
    public class Methods
    {
        private string repo;

        private List<Models.FileInfoJson> finalJson = new List<FileInfoJson>();

        private List<Models.FileInfoJson> fileInfoJsons = new List<FileInfoJson>();

        public Methods(string _repo)
        {
            repo = _repo;
        }

        public async Task<string> GetStringAsync(string path)
        {
            HttpClient client = new HttpClient();
            string html = await client.GetStringAsync(repo);
            return html;
        }

        public async Task<List<FileInfoJson>> ProcessarAsync()
        {
            HttpClient client = new HttpClient();
            string html = await client.GetStringAsync(repo);

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            HtmlNode arquivos = null;

            const string startPath = "//*[@id=\"repo-content-pjax-container\"]/div/div[2]/div[1]/div[2]";
            string endPath = "/div[3]/div[1]";
            string xpath = startPath + endPath;

            arquivos = doc.DocumentNode.SelectSingleNode(xpath);

            if (arquivos == null)
            {
                endPath = "/include-fragment/div[2]/div[1]";
                xpath = startPath + endPath;
                arquivos = doc.DocumentNode.SelectSingleNode(xpath);
            }

            int totalDeArquivos = (arquivos.ChildNodes.Count / 2) - 1;

            HtmlDocument docFile = new HtmlDocument();

            if (arquivos != null)
            {
                docFile.LoadHtml(arquivos.InnerHtml);
            }

            for (int i = 1; i <= totalDeArquivos; i++)
            {
                int id = i + 1;
                
                string gitHub = "https://github.com/";
                string extension = "empity";

                HtmlNode arquivo = null;

                arquivo = docFile.DocumentNode.SelectSingleNode($"/div[{id}]/div[2]/span/a");

                string path = gitHub + arquivo.Attributes[3].Value;
                string title = arquivo.Attributes[1].Value;
                string lineXPath = "//*[@id=\"repo-content-pjax-container\"]/div/div[3]/div[1]/div[1]/text()[1]";
                string bytesXPath = "//*[@id=\"repo-content-pjax-container\"]/div/div[3]/div[1]/div[1]/text()[2]";


                if (title == "LICENSE")
                {
                    path = gitHub + arquivo.Attributes[4].Value;
                    lineXPath = "//*[@id=\"repo-content-pjax-container\"]/div/div[4]/div[1]/div[1]/text()[1]";
                    bytesXPath = "//*[@id=\"repo-content-pjax-container\"]/div/div[4]/div[1]/div[1]/text()[2]";
                }
                else if (title == "README.md")
                {
                    lineXPath = "//*[@id=\"repo-content-pjax-container\"]/div/readme-toc/div/div[1]/div[1]/text()[1]";
                    bytesXPath = "//*[@id=\"repo-content-pjax-container\"]/div/readme-toc/div/div[1]/div[1]/text()[2]";
                }

                HtmlNode infoType = docFile.DocumentNode.SelectSingleNode($"/div[{id}]/div[1]/svg");
                string type = infoType.Attributes[0].Value;


                if (type == "File")
                {
                    var splits = title.Split('.');
                    int totalSplit = splits.Count();
                    if (totalSplit > 0)
                    {
                        extension = splits[totalSplit - 1];
                    }

                    HtmlDocument docLineBytes = new HtmlDocument();

                    string urlFile = await client.GetStringAsync(path);

                    docLineBytes.LoadHtml(urlFile.ToString());

                    HtmlNode infoLine = docLineBytes.DocumentNode.SelectSingleNode(lineXPath);
                    string auxLines = infoLine.OuterHtml;
                    string lines = "";
                    bool confirm = false;

                    for (int l = 0; l < auxLines.Length; l++)
                    {
                        string sub = auxLines.Substring(l, 1);
                        try
                        {
                            Int32.Parse(sub);
                            lines += sub;
                            confirm = true;
                        }
                        catch (Exception)
                        {
                            if (confirm)
                            {
                                break;
                            }

                            confirm = false;
                        }
                    }

                    HtmlNode infoBytes = docLineBytes.DocumentNode.SelectSingleNode(bytesXPath);
                    string auxBytes = infoBytes.OuterHtml;
                    string bytes = "";
                    confirm = false;

                    long mult = 1;

                    if (auxBytes.Contains("KB"))
                    {
                        mult = 1024;

                    }
                    else if (auxBytes.Contains("MB"))
                    {
                        mult = 1024 * 1024;

                    }
                    else if (auxBytes.Contains("GB"))
                    {
                        mult = 1024 * 1024 * 1024;
                    }

                    for (int b = 0; b < auxBytes.Length; b++)
                    {
                        string sub = auxBytes.Substring(b, 1);
                        try
                        {
                            Int32.Parse(sub);
                            bytes += sub;
                            confirm = true;
                        }
                        catch (Exception)
                        {
                            if (confirm)
                            {
                                if (sub == ".")
                                {
                                    bytes += sub;
                                }
                                else
                                {
                                    break;
                                }
                            }

                            confirm = false;
                        }
                    }

                    var fileInfo = new Models.FileInfoJson
                    {
                        Extension = extension,
                        Lines = long.Parse(lines),
                        Bytes = double.Parse(bytes) * mult
                    };

                    fileInfoJsons.Add(fileInfo);

                }
                else
                {
                    //await ProcessarSubPasta(path);
                }
            }

            List<string> typesExtension = new List<string>();

            foreach (var file in fileInfoJsons)
            {
                if (typesExtension.Exists(x => x.Contains(file.Extension)))
                {
                    continue;
                }
                else
                {
                    typesExtension.Add(file.Extension);
                }
            }

            foreach (var ext in typesExtension)
            {
                List<Models.FileInfoJson> tempList = fileInfoJsons.Where(x => x.Extension == ext).ToList();

                Models.FileInfoJson json = new FileInfoJson();
                json.Extension = ext;
                json.Count = tempList.Count();

                foreach (var j in tempList)
                {
                    json.Lines += j.Lines;
                    json.Bytes += j.Bytes;
                }

                finalJson.Add(json);
            }

            return finalJson;

        }

        public async Task ProcessarSubPastaAsync(string directory)
        {
            HttpClient client = new HttpClient();

            string html = await client.GetStringAsync(directory);

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            HtmlNode arquivos = null;

            ////*[@id="repo-content-pjax-container"]/div/div[3]/include-fragment/div[2]/div
            ////*[@id="repo-content-pjax-container"]/div/div[3]/div[3]/div
            const string startPath = "//*[@id=\"repo-content-pjax-container\"]/div/div[3]";
            string endPath = "/div[3]/div";
            string xpath = startPath + endPath;

            arquivos = doc.DocumentNode.SelectSingleNode(xpath);

            if (arquivos == null)
            {
                endPath = "/include-fragment/div[2]/div";
                xpath = startPath + endPath;
                arquivos = doc.DocumentNode.SelectSingleNode(xpath);
            }

            int totalDeArquivos = (arquivos.ChildNodes.Count / 2) - 2;

            HtmlDocument docFile = new HtmlDocument();

            if (arquivos != null)
            {
                docFile.LoadHtml(arquivos.InnerHtml);
            }

            for (int i = 1; i <= totalDeArquivos; i++)
            {
                int id = i;
                int idSubFolder = i + 1;
                idSubFolder++;
                
                string gitHub = "https://github.com/";
                string extension = "empity";

                HtmlNode arquivo = null;
                arquivo = docFile.DocumentNode.SelectSingleNode($"/div[{id}]/div[2]/span/a");

                string path = gitHub + arquivo.Attributes[3].Value;
                string title = arquivo.Attributes[1].Value;
                string lineXPath = "//*[@id=\"repo-content-pjax-container\"]/div/div[3]/div[1]/div[1]/text()[1]";
                string bytesXPath = "//*[@id=\"repo-content-pjax-container\"]/div/div[3]/div[1]/div[1]/text()[2]";


                if (title == "LICENSE")
                {
                    path = gitHub + arquivo.Attributes[4].Value;
                    lineXPath = "//*[@id=\"repo-content-pjax-container\"]/div/div[4]/div[1]/div[1]/text()[1]";
                    bytesXPath = "//*[@id=\"repo-content-pjax-container\"]/div/div[4]/div[1]/div[1]/text()[2]";
                }
                else if (title == "README.md")
                {
                    lineXPath = "//*[@id=\"repo-content-pjax-container\"]/div/readme-toc/div/div[1]/div[1]/text()[1]";
                    bytesXPath = "//*[@id=\"repo-content-pjax-container\"]/div/readme-toc/div/div[1]/div[1]/text()[2]";
                }

                HtmlNode infoType = docFile.DocumentNode.SelectSingleNode($"/div[{id}]/div[1]/svg");
                string type = infoType.Attributes[0].Value;

                if (type == "File")
                {
                    var splits = title.Split('.');
                    int totalSplit = splits.Count();
                    if (totalSplit > 0)
                    {
                        extension = splits[totalSplit - 1];
                    }

                    HtmlDocument docLineBytes = new HtmlDocument();

                    string urlFile = await client.GetStringAsync(path);

                    docLineBytes.LoadHtml(urlFile);

                    HtmlNode infoLine = docLineBytes.DocumentNode.SelectSingleNode(lineXPath);
                    string auxLines = infoLine.OuterHtml;
                    string lines = "";
                    bool confirm = false;

                    for (int l = 0; l < auxLines.Length; l++)
                    {
                        string sub = auxLines.Substring(l, 1);
                        try
                        {
                            Int32.Parse(sub);
                            lines += sub;
                            confirm = true;
                        }
                        catch (Exception)
                        {
                            if (confirm)
                            {
                                break;
                            }

                            confirm = false;
                        }
                    }

                    HtmlNode infoBytes = docLineBytes.DocumentNode.SelectSingleNode(bytesXPath);
                    string auxBytes = infoBytes.OuterHtml;
                    string bytes = "";
                    confirm = false;

                    long mult = 1;

                    if (auxBytes.Contains("KB"))
                    {
                        mult = 1024;

                    }
                    else if (auxBytes.Contains("MB"))
                    {
                        mult = 1024 * 1024;

                    }
                    else if (auxBytes.Contains("GB"))
                    {
                        mult = 1024 * 1024 * 1024;
                    }

                    for (int b = 0; b < auxBytes.Length; b++)
                    {
                        string sub = auxBytes.Substring(b, 1);
                        try
                        {
                            Int32.Parse(sub);
                            bytes += sub;
                            confirm = true;
                        }
                        catch (Exception)
                        {
                            if (confirm)
                            {
                                if (sub == ".")
                                {
                                    bytes += sub;
                                }
                                else
                                {
                                    break;
                                }
                            }

                            confirm = false;
                        }
                    }

                    var fileInfo = new Models.FileInfoJson
                    {
                        Extension = extension,
                        Lines = long.Parse(lines),
                        Bytes = double.Parse(bytes) * mult
                    };

                    fileInfoJsons.Add(fileInfo);

                }
                else
                {
                    ProcessarSubPastaAsync(path);
                }



            }

        }
    }
}
