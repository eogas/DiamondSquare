using System;

namespace DiamondSquare
{
#if WINDOWS || XBOX
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            using (DiamondSquare game = new DiamondSquare())
            {
                game.Run();
            }
        }
    }
#endif
}

