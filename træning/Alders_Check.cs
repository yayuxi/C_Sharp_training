// See https://aka.ms/new-console-template for more information
namespace  træning;
class Alders_Check
{
    public static void AldersCheck()
    {
        Console.WriteLine("Alders check!\nVenligst indtast din alder for at se om du er myndig til at stemme:\t");
        string input = Console.ReadLine();
        try {
            int age = Convert.ToInt32(input);

            if (age < 0) {
                throw new Exception("Ugyldig alder! Indtast en gyldig alder.");
            } else if (age > 150) {
                throw new Exception("Din alder er for høj for et menneske, indtast en realistisk alder.");
            }
    
            if (age < 18) {
                Console.WriteLine("Du er under 18 år og derfor ikke myndig");
            } else {
                Console.WriteLine("Du er myndig");
            }
        } catch (Exception e) {
            Console.WriteLine(e);
        }
    }
}
