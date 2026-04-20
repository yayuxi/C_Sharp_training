namespace BooksScraper.Models;

public record Book {
    public string Title { get; set; }
    public double Price { get; set; }
    public int StarRating { get; set; }
    public bool Availability { get; set; }
    public string Category { get; set; }
}