using HtmlAgilityPack;
using Newtonsoft.Json;
using ClosedXML.Excel;


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
	
	public void MakePartXLSX(string code, List<string> models)
	{
		var workbook = new XLWorkbook();
		var worksheet = workbook.Worksheets.Add("Items");

		worksheet.Cell("A" + 1).Value = "#";
		worksheet.Cell("B" + 1).Value = "Item code";
		
		foreach ((int key, string modelCode) in models.Index())
		{
			worksheet.Cell("A" + key + 2).Value = key + 1;
			worksheet.Cell("B" + key + 2).Value = modelCode;
		}
		
		worksheet.Columns().AdjustToContents();
		
		workbook.SaveAs($"{FILE_DIR}{code}.xlsx");
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
	
	protected string _httpRequest(string url, string method = "GET", Dictionary<string, string> formData = null)
	{
		var request = new HttpRequestMessage(method == "GET" ? HttpMethod.Get : HttpMethod.Post, url);

		if (formData is not null)
		{
			request.Content = new FormUrlEncodedContent(formData);
		}
		Console.WriteLine(url);
		return httpClient.Send(request).Content.ReadAsStringAsync().Result;
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