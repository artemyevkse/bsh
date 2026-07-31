public class Model
{
	public required int id { get; set; }
	public required string code { get; set; }
	public required int specifications { get; set; }
	public required int documents { get; set; }
	public DateTime date { get; set; }
	
	public bool xlsxExists = false;
	public bool zipExists = false;
}