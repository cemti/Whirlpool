string input = Console.ReadLine()!;
byte[] digest = [.. new Whirlpool(input).Digest];
string hex = BitConverter.ToString(digest).Replace("-", "");
Console.WriteLine(hex);