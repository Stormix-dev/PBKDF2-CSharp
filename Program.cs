using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

class PBKDF2Threaded
{
    private const int Iterations = 4096;
    private const int KeyLength = 256;

    public static string HashPassword(string password, byte[] salt)
    {
        using (var rfc2898DeriveBytes = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
        {
            byte[] hash = rfc2898DeriveBytes.GetBytes(KeyLength / 8);
            StringBuilder hexString = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                hexString.AppendFormat("{0:x2}", b);
            }
            return hexString.ToString().Substring(0, 32);
        }
    }

    static void Main(string[] args)
    {
        string inputFileReti = "<Inserire percorso dove è presente il file reti.txt>";
        string inputFilePasswords = "<Inserire il percorso dove è presente il file passwords.txt>";
        string outputFile = "<Inserire il percorso per il file di output che verrà generato>";

        List<string> reti = new List<string>();
        List<string> passwords = new List<string>();

        // Leggi i file in parallelo
        Task readReti = Task.Run(() => { ReadFile(inputFileReti, reti); });
        Task readPasswords = Task.Run(() => { ReadFile(inputFilePasswords, passwords); });
        Task.WaitAll(readReti, readPasswords);

        // Inizio del timer
        var startTime = DateTime.Now;

        List<string> results = new List<string>(reti.Count * passwords.Count);

        // Parallelizzare con il numero di core disponibili
        Parallel.ForEach(reti, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, rete =>
        {
            byte[] salt = Encoding.UTF8.GetBytes(rete);
            List<string> localResults = new List<string>(passwords.Count); // Pre-alloca la lista per le password

            // Usa StringBuilder per ridurre le allocazioni di memoria
            StringBuilder localResultBuilder = new StringBuilder(passwords.Count * 64);
            foreach (var password in passwords)
            {
                string hashed = HashPassword(password, salt);
                localResultBuilder.AppendLine(hashed);
            }

            // Aggiungi i risultati per ogni thread (usa lock per evitare conflitti)
            lock (results)
            {
                results.Add(localResultBuilder.ToString());
            }
        });

        // Scrivi tutti i risultati in un colpo solo per ottimizzare la scrittura
        try
        {
            File.WriteAllLines(outputFile, results);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Error writing file: {ex.Message}");
        }

        // Fine del timer
        var endTime = DateTime.Now;
        var duration = endTime - startTime; // durata in millisecondi
        Console.WriteLine("Tempo impiegato: " + duration.TotalMilliseconds + " ms");
    }

    static void ReadFile(string filePath, List<string> lines)
    {
        try
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lock (lines)
                    {
                        lines.Add(line);
                    }
                }
            }
        }
        catch (IOException e)
        {
            Console.WriteLine("Error reading file: " + e.Message);
        }
    }
}
