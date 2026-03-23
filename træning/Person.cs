namespace træning;

public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
    

  
    
    public virtual void Greet() {
        Console.WriteLine($"Hej {Name}, tillykke med dine {Age} år.");
    }
}
public class Developer : Person {
    public string FavoriteLanguage { get; set; }

    public override void Greet() {
        Console.WriteLine($"Hej {Name}, tillykke med dine {Age} år. Dit favorit programmeringssprog er {FavoriteLanguage}.");
    }
}