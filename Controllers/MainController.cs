using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters; // ActionExecutingContext


public class MainController: Controller
{
	const string URL_HOST_ONLINE = "https://91.239.206.123:17716";

	
	public override void OnActionExecuting(ActionExecutingContext context) // Microsoft.AspNetCore.Mvc.Filters
	{
		ViewBag.onlineHost = URL_HOST_ONLINE;
	}
	
	public IActionResult Index([FromServices] BshService bshService)
	{
		return View(bshService.GetParts());
	}
	
	public IActionResult Models([FromServices] BshService bshService)
	{
		return View(bshService.GetModels());
	}
	
	public IActionResult Videos([FromServices] BshService bshService)
	{
		return View(bshService.GetVideos());
	}
	
	public IActionResult FindPart([FromServices] BshService bshService)
	{
		if (HttpContext.Request.Method == "POST")
		{
			string code = HttpContext.Request.Form["code"];
			
			if (!string.IsNullOrEmpty(code))
			{
				Part p = bshService.GetPart(code);
				
				if (p is null)
				{
					bshService.Login();
					
					List<string> models = bshService.onlinePartInfo(code);
					if (models.Count > 0)
					{
						bshService.MakePartXLSX(code, models);
						bshService.AddPart(code, models.Count);
					} else
					{
						Console.WriteLine("not found");
					}					
				} else
				{
					Console.WriteLine("code already exists");
				}				
			}
		}
			
		return Redirect("/index/");
	}
	
	[HttpGet("/deletePart/{id:int}")] 
	public IActionResult DeletePart(int id, [FromServices] BshService bshService)
	{
		bshService.DeletePart(id);
		
		return Redirect("/index/");
	}
	
	public IActionResult FindModel([FromServices] BshService bshService)
	{
		if (HttpContext.Request.Method == "POST")
		{
			string code = HttpContext.Request.Form["code"];
			
			if (!string.IsNullOrEmpty(code))
			{
				Model p = bshService.GetModel(code);
				
				if (p is null)
				{
					bshService.Login();
					
					//var specItems = bshService.onlineModelSpec(code);
										
				} else
				{
					Console.WriteLine("code already exists");
				}				
			}
		}
			
		return Redirect("/index/");
	}
}