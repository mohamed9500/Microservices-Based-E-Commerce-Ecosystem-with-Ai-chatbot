using System;

class Program
{
    static void Main()
    {
       
        Random random = new Random();
        int targetNumber = random.Next(1, 101);

        int userGuess = 0;
        int attempts = 0;
        bool isCorrect = false;

        Console.WriteLine("=== Number Guessing Game ===");
        Console.WriteLine("I have chosen a random number between 1 and 100. Try to guess it!");

      
        while (!isCorrect)
        {
            Console.Write("\nEnter your guess: ");
            string input = Console.ReadLine();

            // 3. Validate input using int.TryParse
            if (!int.TryParse(input, out userGuess))
            {
                Console.WriteLine("Invalid input! Please enter a valid integer.");
                continue;
            }

            attempts++;

            // 4. Conditions for higher / lower / correct hints
            if (userGuess < targetNumber)
            {
                Console.WriteLine("Higher! Try again.");
            }
            else if (userGuess > targetNumber)
            {
                Console.WriteLine("Lower! Try again.");
            }
            else
            {
                Console.WriteLine($"\nCongratulations! You guessed the correct number: {targetNumber}");
                Console.WriteLine($"Total attempts: {attempts}");
                isCorrect = true;
            }
        }
    }
}