using HtmlAgilityPack;
using Newtonsoft.Json;
using ClosedXML.Excel;


public struct ModelDocument
{
	public string name { get; set; }
	public string desc { get; set; }
	public string lang { get; set; }
	public string url { get; set; }
	public string number { get; set; }
}


public class BshService(ApplicationDBContext db)
{
	const string LOGIN = "0200100536_TE5";
	const string PASSWORD = "6lOFuN-AP7";
	
	const string BASE_URL = "https://login.bsh-partner.com";
	const string B2B_URL = "https://b2bportal-cloud.bsh-partner.com";
	const string ESP_URL = "https://esp.bsh-partner.com/nesp/app/plogin?agAppNa=pEtip-odata"
        + "&c=secure/name/password/uri&target=%22https://b2bportal-cloud.bsh-partner.com/sap/bc/"
        + "ui5_ui5/bshb2b/ca_apc/cloudLogin.html?";
	const string PIP_URL = "https://pip.bsh-partner.com";
	const string CLIPSER_URL = "https://mycliplister.com";
	
	const string FILE_DIR = "wwwroot/files/";

	private HttpClient httpClient = new HttpClient();
	

	
	public List<Part> GetParts()
	{
		var parts = db.parts.ToList();
		
		foreach (Part part in parts)
		{
			part.xlsxExists = File.Exists($"{FILE_DIR}{part.code}.xlsx");
		}

		return parts;
	}

	public List<Model> GetModels()
	{
		var models = db.models.ToList();
		
		foreach (Model model in models)
		{
			string modelCodeOS = model.code.Replace("/", "_");
			model.xlsxExists = File.Exists($"{FILE_DIR}{modelCodeOS}/specifications.xlsx");
			model.zipExists = File.Exists($"{FILE_DIR}{modelCodeOS}.zip");
		}
		
		return models;
	}

	public List<Video> GetVideos() => db.videos.ToList();
	
	public void AddPart(string code, int modelCount)
	{
		var part = new Part {code = code, items = modelCount};
		db.parts.Add(part);
		db.SaveChanges();
	}
	
	public void DeletePart(int id)
	{
		db.parts.Where(p => p.id == id).ExecuteDelete();
	}
	
	public Part GetPart(string code)
	{
		return db.parts.FirstOrDefault(p => p.code == code);
	}
	
	public Model GetModel(string code)
	{
		return db.models.FirstOrDefault(m => m.code == code);
	}
	
	public Video GetVideo(string videoId)
	{
		return db.videos.FirstOrDefault(v => v.videoId == videoId);
	}

	public void Login()
	{
		var payload = new Dictionary<string, string>
		{
			{"Ecom_User_ID", LOGIN},
			{"Ecom_Password", PASSWORD}
		};

		_httpRequest($"{BASE_URL}/nidp/app/login", "GET");
		string action = _getAttributeXPath(
			_httpRequest($"{BASE_URL}/nidp/app/portal", "GET"),
			"//form[@method='POST']", "action"
		);
		action = _getAttributeXPath(
			_httpRequest($"{BASE_URL}{action}", "POST"),
			"//*[@name='IDPLogin']", "action"
		);
		_httpRequest(_strBetween(_getInnerTextXPath(_httpRequest($"{action}", "POST", payload), "//script"), "'", "'"), "GET");

		_payloadSAML(_httpRequest(B2B_URL), ["SAMLResponse", "RelayState"]);

		_payloadSAML(
			_payloadSAML(_httpRequest(ESP_URL + Random.Shared.NextInt64(1111111111, 9999999999)), ["SAMLRequest", "RelayState"]),
			["SAMLResponse", "RelayState"]
		);

		var finishContent = _payloadSAML(_httpRequest(PIP_URL
			+ "/picenter/xport/login?userid=X0000102479&username=test&locale=ru_RU&brandid=A00&customerid=200100536&country=RU"
		), ["SAMLResponse"]); Console.WriteLine(finishContent);
	}
	
	public List<string> onlinePartInfo(string code)
	{
		dynamic stuff = JsonConvert.DeserializeObject(
			_httpRequest(PIP_URL +$"/picenter/ui/product?vib={code}&productRelation=usedIn")
		);
		return stuff.used_in.ToObject<List<string>>();
	}
	
	public JArray onlineModelSpec(string code)
	{
		JObject stuff = JObject.Parse(_httpRequest(PIP_URL +$"/picenter/ui/productversions?vibKi={code}&view=jsonParts"));
		if (stuff is null || stuff["product_data"] is null) { return null; }
		
		return (JArray)stuff["product_data"];
	}
	
	public (Dictionary<string, List<ModelDocument>>, List<ModelDocument>) onlineModelDocuments(string code)
	{
		Dictionary<string, List<ModelDocument>> documents = [];
		List<ModelDocument> videos = [];
		
		JObject stuff = JObject.Parse(_httpRequest(PIP_URL +$"/picenter/ui/productversions?vibKi={code}&view=jsonDocuments"));
		if (stuff is null || stuff["documents"] is null) { return (null, null); }

		foreach (KeyValuePair<string, JToken> item in (JObject)stuff["documents"])
		{
			foreach (var doc in item.Value["files"])
			{
				if (!(bool)doc["isVideo"])
				{
					foreach (var lang in doc["lang"])
					{
						string l = ((string)lang["name"])[0..2];

						if (new []{"RU", "EN"}.Contains(l))
						{
							if (!documents.ContainsKey(item.Key)) { documents.Add(item.Key, []); }
							
							documents[item.Key].Add(new ModelDocument {
								name = (string)doc["name"],
								desc = (string)doc["desc"],
								lang = (string)lang["name"],
								url = PIP_URL + "/picenter/ui/technicaldocument?format=zip&" + (string)lang["url"],
								number = (string)doc["documentNumber"]
							});
						}
					}
				} else
				{
					videos.Add(new ModelDocument {
						name = (string)doc["name"],
						desc = (string)doc["desc"],
						number = (string)doc["videoId"]
					});
				}
				
			}
			
		}

		return (documents, videos);
	}
	
	public int MakeModelDocumentsArchive(string code, Dictionary<string, List<ModelDocument>> documents)
	{
		string modelDir = code.Replace("/", "_");

		var workbook = new XLWorkbook();
		var worksheet = workbook.Worksheets.Add("Documents");
		worksheet.Cell("A" + 1).Value = "#";
		worksheet.Cell("B" + 1).Value = "Document Number";
		worksheet.Cell("C" + 1).Value = "Name";
		worksheet.Cell("D" + 1).Value = "Description";
		worksheet.Cell("E" + 1).Value = "Language";
		
		var countRes = 0;
		
		foreach (var category in documents)
		{
			List<string> docs = [];
			foreach (var document in category.Value)
			{
				if (!docs.Contains(document.number))
				{
					docs.Add(document.number);
					countRes++;
					worksheet.Cell("A" + (countRes + 1)).Value = countRes;
					worksheet.Cell("B" + (countRes + 1)).Value = document.number;
					worksheet.Cell("C" + (countRes + 1)).Value = document.name;
					worksheet.Cell("D" + (countRes + 1)).Value = document.desc;
					worksheet.Cell("E" + (countRes + 1)).Value = document.lang;
					
					_DownloadFile(FILE_DIR + modelDir + "/" + category.Key, document.number + ".zip", document.url);
				}
			}
		}
		
		worksheet.Columns().AdjustToContents();
		workbook.SaveAs($"{FILE_DIR}{modelDir}/documents.xlsx");
		
		var archivePath = $"{FILE_DIR}{modelDir}.zip";
		
		if (File.Exists(archivePath))
		{
			File.Delete(archivePath);
		}

		System.IO.Compression.ZipFile.CreateFromDirectory($"{FILE_DIR}{modelDir}", archivePath);
		
		return countRes;
	}
	
	public void MakePartXLSX(string code, List<string> models)
	{
		var workbook = new XLWorkbook();
		var worksheet = workbook.Worksheets.Add("Items");

		worksheet.Cell("A" + 1).Value = "#";
		worksheet.Cell("B" + 1).Value = "Item code";
		
		foreach ((int key, string modelCode) in models.Index())
		{
			worksheet.Cell("A" + (key + 2)).Value = key + 1;
			worksheet.Cell("B" + (key + 2)).Value = modelCode;
		}
		
		worksheet.Columns().AdjustToContents();
		
		workbook.SaveAs($"{FILE_DIR}{code}.xlsx");
	}
	
	public void MakeModelXLSX(string code, JArray items)
	{
		var workbook = new XLWorkbook();
		var worksheet = workbook.Worksheets.Add("Parts");
		
		worksheet.Cell("A" + 1).Value = "#";
		worksheet.Cell("B" + 1).Value = "ID";
		worksheet.Cell("C" + 1).Value = "Parts #";
		worksheet.Cell("D" + 1).Value = "In use";
		worksheet.Cell("E" + 1).Value = "Description";
		
		int key = 0;
		foreach (var item in items)
		{
			worksheet.Cell("A" + (key + 2)).Value = key + 1;
			worksheet.Cell("B" + (key + 2)).Value = item["posnr"].ToString();
			worksheet.Cell("C" + (key + 2)).Value = item["matnr"].ToString();
			string successor = (item["successor"] is null) ? "" : item["successor"]["matnr"].ToString();
			worksheet.Cell("D" + (key + 2)).Value = successor;			
			worksheet.Cell("E" + (key + 2)).Value = item["maktx"].ToString();
			key++;			
		}
		
		worksheet.Columns().AdjustToContents();
		
		string modelDir = code.Replace("/", "_");
		Directory.CreateDirectory($"{FILE_DIR}{modelDir}");
		workbook.SaveAs($"{FILE_DIR}{modelDir}/specifications.xlsx");
	}
	
	public int AddVideos(List<ModelDocument> videos, string modelCode)
	{
		Dictionary<string, string> headers = new Dictionary<string, string>
		{
			{"Origin", PIP_URL},
			{"Referer", PIP_URL + "/"}			
		};
		
		string cataa = Convert.ToBase64String(
			System.Text.Encoding.UTF8.GetBytes(PIP_URL + $"/picenter/ui/productversions?vibKi={modelCode}")
		);		
		
		int countNewVideos = 0;
		foreach (var v in videos)
		{ Console.WriteLine((string)v.number);
			if (GetVideo(v.number) is not null) { continue; }

			string encoded = Convert.ToBase64String(
				System.Text.Encoding.UTF8.GetBytes($"[{{\"SEO\":false,\"keytype\":10000,\"requestkey\":\"{v.number}\",\"streamtype\":\"hls\"}}]")
			);

			JToken stuff = JToken.Parse(_httpRequest(CLIPSER_URL + $"/lc/81514/?{encoded}", "GET", null, headers));

			if (stuff.Type == JTokenType.Array && (stuff as JArray).Count > 0)
			{
				string videoRequest = (string)stuff[0]["request"];
				
				JToken videoClipser = _httpRequest(CLIPSER_URL + $"/jplist/81514/{videoRequest}?{cataa}", "GET", null, headers);

				Console.WriteLine(JsonConvert.SerializeObject(videoClipser, Formatting.Indented));
			}
		}
		
		return countNewVideos;
	}
	
	protected string _payloadSAML(string content, List<string> parameters)
	{
		var htmlAgilityDoc = _htmlAgilityDoc(content);

		var url = _getAttributeXPath(htmlAgilityDoc, "//form[@method='POST']", "action");		
		var payload = new Dictionary<string, string>() {};
		
		foreach (var p in parameters)
		{
			payload.Add(p, _getAttributeXPath(htmlAgilityDoc, $"//*[@name='{p}']", "value"));
		}

		return _httpRequest(url, "POST", payload);
	}
	
	protected string _httpRequest(string url, string method = "GET", Dictionary<string, string> formData = null,
		Dictionary<string, string> headers = null)
	{
		var request = new HttpRequestMessage(method == "GET" ? HttpMethod.Get : HttpMethod.Post, url);

		if (formData is not null)
		{
			request.Content = new FormUrlEncodedContent(formData);
		}
		
		if (headers is not null)
		{
			foreach (var h in headers) { request.Headers.Add(h.Key, h.Value); }
		}
		
		Console.WriteLine(url);
		return httpClient.Send(request).Content.ReadAsStringAsync().Result;
	}
	
	protected void _DownloadFile(string dir, string filename, string url)
	{
		Directory.CreateDirectory(dir);
		
		var request = new HttpRequestMessage(HttpMethod.Get, url);
		
		File.WriteAllBytes(dir + "/" + filename, 
			httpClient.Send(request).Content.ReadAsByteArrayAsync().Result
		);
	}
	
	protected HtmlDocument _htmlAgilityDoc(string content)
	{
		var doc = new HtmlDocument();
		doc.LoadHtml(content);
		
		return doc;
	}	
	protected string _getAttributeXPath(HtmlDocument doc, string xPath, string attributeName)
		=> HtmlEntity.DeEntitize(doc.DocumentNode.SelectSingleNode(xPath).Attributes[attributeName].Value);
	protected string _getAttributeXPath(string content, string xPath, string attributeName)
		=> _getAttributeXPath(_htmlAgilityDoc(content), xPath, attributeName);	
	protected string _getInnerTextXPath(string content, string xPath)
		=> _htmlAgilityDoc(content).DocumentNode.SelectSingleNode(xPath).InnerText;
	
	protected string _strBetween(string str, string substr1, string substr2)
	{
		int start = str.IndexOf(substr1) + 1;
		int end = str.IndexOf(substr2, start);
		
		if (start > 0 && end > start){
			return str[start..end];
		}
		
		return "";
	}
}