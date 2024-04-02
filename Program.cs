string input = Console.ReadLine()!;
byte[] digest = Whirlpool.Hash(input);
string hex = BitConverter.ToString(digest).Replace("-", "");
Console.WriteLine(hex);