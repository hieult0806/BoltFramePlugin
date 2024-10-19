using System;
using System.IO;
using System.Collections.Generic;

namespace FolderStructureLister
{
    class Program
    {
        static void Main(string[] args)
        {
            // Set the directory to start from (this is the part you want to exclude from the output)
            string rootDirectory = "C:\\Users\\hieul\\source\\repos\\BoltFramePlugin\\BoltFramePlugin";

            // Define the file extensions to include (lowercase)
            List<string> allowedExtensions = new List<string> { ".cs", ".xaml" }; // Add more as needed

            // Define the folders to exclude
            List<string> excludedFolders = new List<string> { "bin", "obj" }; // Add more as needed

            // Create a file to store the results
            string outputFile = Path.Combine(rootDirectory, "folder_structure.txt");

            // Ensure the output file is clean
            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }

            try
            {
                // Create and populate the folder structure
                using (StreamWriter writer = new StreamWriter(outputFile))
                {
                    Console.WriteLine("Processing directory structure...");
                    ListDirectory(rootDirectory, writer, allowedExtensions, excludedFolders, rootDirectory);
                }

                Console.WriteLine($"Folder structure with filtered file types and excluded folders has been saved to {outputFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        // Function to list the directory structure recursively
        static void ListDirectory(string directory, StreamWriter writer, List<string> allowedExtensions, List<string> excludedFolders, string rootDirectory)
        {
            try
            {
                // Check if the current folder is in the excluded list
                foreach (string folder in excludedFolders)
                {
                    if (directory.Contains(Path.DirectorySeparatorChar + folder + Path.DirectorySeparatorChar) ||
                        directory.EndsWith(Path.DirectorySeparatorChar + folder))
                    {
                        Console.WriteLine($"Skipping excluded folder: {directory}");
                        return;
                    }
                }

                // List all files in the current directory, filtering by extension
                foreach (string file in Directory.GetFiles(directory))
                {
                    string extension = Path.GetExtension(file).ToLower();
                    if (allowedExtensions.Contains(extension))
                    {
                        // Create a relative path that excludes the root directory part
                        string relativePath = file.Substring(rootDirectory.Length + 1); // "+1" to remove the leading separator
                        writer.WriteLine(relativePath);
                        Console.WriteLine($"Added file: {relativePath}");
                    }
                }

                // Recursively list subdirectories
                foreach (string subdirectory in Directory.GetDirectories(directory))
                {
                    ListDirectory(subdirectory, writer, allowedExtensions, excludedFolders, rootDirectory);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip folders we don't have access to
                Console.WriteLine($"Access denied to folder: {directory}");
            }
            catch (Exception ex)
            {
                // Handle any other unexpected exceptions
                Console.WriteLine($"Error processing folder: {directory}. Exception: {ex.Message}");
            }
        }
    }
}
