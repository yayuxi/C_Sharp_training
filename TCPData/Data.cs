namespace TCPData;

public static class Data
{
    public static List<Products> GetProducts() {
        return ProductList;
    }
    
    public static List<Products> ProductList = new List<Products>
    {
        new Products { Name = "Laptop", Price = 7999, Category = "Electronics" },
        new Products { Name = "Television", Price = 9999, Category = "Electronics" },
        new Products { Name = "Smartphone", Price = 4995, Category = "Electronics" },
        new Products { Name = "Headphones", Price = 499, Category = "Electronics" },
        new Products { Name = "Coffee Maker", Price = 799, Category = "Home Appliances" },
        new Products { Name = "Blender", Price = 599, Category = "Home Appliances" },
        new Products { Name = "Vacuum Cleaner", Price = 449, Category = "Home Appliances" },
        new Products { Name = "T-Shirt", Price = 79, Category = "Clothing" },
        new Products { Name = "Jeans", Price = 99, Category = "Clothing" },
        new Products { Name = "Sneakers", Price = 289, Category = "Clothing" },
        new Products { Name = "Chair", Price = 99, Category = "Furniture" },
        new Products { Name = "Table",  Price = 299, Category = "Furniture" },
        new Products { Name = "Closet", Price = 399, Category = "Furniture" },
        new Products { Name = "Bed",  Price = 499, Category = "Furniture" },
        new Products { Name = "Piano", Price = 19999, Category = "Instrument"},
        new Products { Name = "Violin", Price = 4999, Category = "Instrument"},
        new Products { Name = "Flute", Price = 2999, Category = "Instrument"},
        new Products { Name = "Guitar", Price = 7999, Category = "Instrument"},
        new Products { Name = "Drum Set", Price = 9999, Category = "Instrument"},
        new Products { Name = "Saxophone", Price = 5999, Category = "Instrument"},
        new Products { Name = "Trumpet", Price = 3999, Category = "Instrument"},
        new Products { Name = "Cello", Price = 8999, Category = "Instrument"},
        new Products { Name = "Apple", Price = 2, Category = "Fruit"},
        new Products { Name = "Banana", Price = 1, Category = "Fruit"},
        new Products { Name = "Orange", Price = 3, Category = "Fruit"},
        new Products { Name = "Mango", Price = 10, Category = "Fruit"},
        new Products { Name = "Grapes", Price = 15,  Category = "Fruit"},
        new Products { Name = "Milk", Price = 12, Category = "Dairy"},
        new Products { Name = "Cheese", Price = 30, Category = "Dairy"},
        new Products { Name = "Butter", Price = 20, Category = "Dairy"},
    }; 
}