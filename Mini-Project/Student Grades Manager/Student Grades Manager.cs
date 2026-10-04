using System;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== Student Grades Manager ===");
        List<double> marksList = GetStudentMarks();

        if (marksList.Count == 0)
        {
            Console.WriteLine("No marks entered. Exiting program.");
            return;
        }

      
        double[] marksArray = marksList.ToArray();
        double average = CalculateAverage(marksArray);
        double highest = GetHighestMark(marksArray);
        double lowest = GetLowestMark(marksArray);

        DisplayResults(marksArray, average, highest, lowest);
    }

    static List<double> GetStudentMarks()
    {
        List<double> marks = new List<double>();
        Console.WriteLine("Enter student marks (0 to 100). Type 'done' when finished:");

        while (true)
        {
            Console.Write("Enter mark: ");
            string input = Console.ReadLine();

            if (input.Trim().ToLower() == "done")
                break;

            if (double.TryParse(input, out double mark) && mark >= 0 && mark <= 100)
            {
                marks.Add(mark);
            }
            else
            {
                Console.WriteLine("Invalid input! Please enter a number between 0 and 100.");
            }
        }

        return marks;
    }

    static double CalculateAverage(double[] marks)
    {
        double sum = 0;
        foreach (double mark in marks)
        {
            sum += mark;
        }
        return sum / marks.Length;
    }

    static double GetHighestMark(double[] marks)
    {
        double max = marks[0];
        foreach (double mark in marks)
        {
            if (mark > max) max = mark;
        }
        return max;
    }

    static double GetLowestMark(double[] marks)
    {
        double min = marks[0];
        foreach (double mark in marks)
        {
            if (mark < min) min = mark;
        }
        return min;
    }

    static void DisplayResults(double[] marks, double average, double highest, double lowest)
    {
        Console.WriteLine("\n==============================");
        Console.WriteLine("        SUMMARY REPORT        ");
        Console.WriteLine("==============================");

        Console.WriteLine("\nIndividual Student Results:");
        int studentNumber = 1;

        foreach (double mark in marks)
        {
            string status = mark >= 50 ? "PASS" : "FAIL";
            Console.WriteLine($"Student {studentNumber}: {mark} -> [{status}]");
            studentNumber++;
        }

        Console.WriteLine("------------------------------");
        Console.WriteLine($"Total Students : {marks.Length}");
        Console.WriteLine($"Average Grade  : {average:F2}");
        Console.WriteLine($"Highest Mark   : {highest}");
        Console.WriteLine($"Lowest Mark    : {lowest}");
        Console.WriteLine("==============================");
    }
}
