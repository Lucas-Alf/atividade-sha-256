using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace atividade_sha_256
{
    class Program
    {
        static void Main(string[] args)
        {
            Int64 min = 0;
            Int64 max = 12345679;
            Int64 result = 0;

            //12345678
            string hashToFind = "ef797c8118f02dfb649607dd5d3f8c7623048c9c063d532cc95c5ed7a898a64f";

            Parallel.For(min, max, new ParallelOptions() { MaxDegreeOfParallelism = 4 }, (Int64 i, ParallelLoopState state) =>
              {
                  string hash = "";
                  var crypt = new SHA256Managed();
                  byte[] crypto = crypt.ComputeHash(Encoding.ASCII.GetBytes(i.ToString()));
                  foreach (byte theByte in crypto)
                  {
                      hash += theByte.ToString("x2");
                  }
                  if (hash == hashToFind)
                  {
                      result = i;
                      state.Stop();
                  }
                //   Console.WriteLine(i + "");
              });
            Console.WriteLine("ENCONTRADO: " + result.ToString());
        }
    }
}
