namespace træning;

public class Contact
{
    public string Name { get; set; }
    public string Email { get; set; }
    
    List<Contact> Contacts = new List<Contact>();

    public Contact(string name, string email) {
        Name = name;
        Email = email;
    }

    public void addContact(string name, string email) {
        Contacts.Add(new Contact(name, email));
    }

    public void removeContact(string name) {
        Contacts.RemoveAt(Contacts.FindIndex(x => x.Name == name));
    }
    
    public void printList() {
        foreach (var contact in Contacts) {
            Console.WriteLine(contact.Name + "\t" + contact.Email);
        }
    }
    
    public void searchContact(string name) {
        foreach (var contact in Contacts) {
            if (contact.Name == name) {
                Console.WriteLine(contact.Name + "\t" + contact.Email);
            }
        }
    }
    
    public void exitProgram() {
        Console.WriteLine("Tak for at bruge vores kontaktbog! Vi ses næste gang!");
        Environment.Exit(0);
    }
}