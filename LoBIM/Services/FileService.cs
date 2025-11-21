using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using LoBIM.Models;
using Newtonsoft.Json;

namespace LoBIM.Services
{
    public class FileService : IFileService
    {
        /// <inheritdoc />
        public bool Exists(string path)
        {
            try
            {
                return File.Exists(path);
            }
            catch (Exception ex)
            {
                // Optionally log the exception using your logging framework
                // For example: _logger.LogError(ex, $"Error checking existence of file: {path}");
                throw new IOException($"Failed to check if file exists: {path}", ex);
            }
        }

        /// <inheritdoc />
        public string ReadAllText(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                // Optionally log the exception
                throw new IOException($"Failed to read file: {path}", ex);
            }
        }

        /// <inheritdoc />
        public void WriteAllText(string path, string content)
        {
            try
            {
                // Ensure the directory exists
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, content);
            }
            catch (Exception ex)
            {
                // Optionally log the exception
                throw new IOException($"Failed to write to file: {path}", ex);
            }
        }

        /// <inheritdoc />
        public void Copy(string sourcePath, string destinationPath, bool overwrite)
        {
            try
            {
                // Ensure the source file exists
                if (!File.Exists(sourcePath))
                {
                    throw new FileNotFoundException($"Source file not found: {sourcePath}");
                }

                // Ensure the destination directory exists
                string destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(sourcePath, destinationPath, overwrite);
            }
            catch (Exception ex)
            {
                // Optionally log the exception
                throw new IOException($"Failed to copy file from {sourcePath} to {destinationPath}", ex);
            }
        }

        /// <inheritdoc />
        public void CreateDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
            catch (Exception ex)
            {
                // Optionally log the exception
                throw new IOException($"Failed to create directory: {path}", ex);
            }
        }

        /// <inheritdoc />
        public void DeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                // Optionally log the exception
                throw new IOException($"Failed to delete file: {path}", ex);
            }
        }

        /// <inheritdoc />
        public void MoveFile(string sourcePath, string destinationPath)
        {
            try
            {
                // Ensure the source file exists
                if (!File.Exists(sourcePath))
                {
                    throw new FileNotFoundException($"Source file not found: {sourcePath}");
                }

                // Ensure the destination directory exists
                string destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Move(sourcePath, destinationPath);
            }
            catch (Exception ex)
            {
                // Optionally log the exception
                throw new IOException($"Failed to move file from {sourcePath} to {destinationPath}", ex);
            }
        }
    }
}
