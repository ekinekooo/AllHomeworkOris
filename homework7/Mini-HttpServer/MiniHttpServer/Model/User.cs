namespace MiniHttpServer.Model
{
    internal class User
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";

        public string LastName { get; set; } = "";

        public int Age { get; set; }

        public string Password { get; set; } = "";

        public string Email { get; set; } = "";

        public override string ToString()
        {
            return $"{Id} - {Name} {LastName} ({Email})";
        }
    }
}
