using System.ComponentModel.DataAnnotations.Schema;


public class Part
{
	[Column("id")]
	public int id { get; set; }
	public required string code { get; set; }
	public DateTime date { get; set; } = DateTime.Now;
	public int items { get; set; } = 0;
	
	public bool xlsxExists = false;
}