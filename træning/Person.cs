namespace træning;

public class Person
{
    private string Name; 
    private int Age;

    public Person(string name, int age) {
        this.Name = name; this.Age = age;
    }

    public void Greet() {
        Console.WriteLine($"Hej {Name}, tillykke med dine {Age} år.");
    }
}