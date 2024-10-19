using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoltFramePlugin.Services
{
    public interface IFileService
    {
        /// <summary>
        /// Checks if the specified file exists.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>True if the file exists; otherwise, false.</returns>
        bool Exists(string path);

        /// <summary>
        /// Reads all text from the specified file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>The file's contents as a string.</returns>
        string ReadAllText(string path);

        /// <summary>
        /// Writes the specified content to the file, creating or overwriting it.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="content">The content to write.</param>
        void WriteAllText(string path, string content);

        /// <summary>
        /// Copies a file to a new location.
        /// </summary>
        /// <param name="sourcePath">The source file path.</param>
        /// <param name="destinationPath">The destination file path.</param>
        /// <param name="overwrite">Whether to overwrite the destination file if it exists.</param>
        void Copy(string sourcePath, string destinationPath, bool overwrite);

        /// <summary>
        /// Creates all directories and subdirectories in the specified path.
        /// </summary>
        /// <param name="path">The directory path.</param>
        void CreateDirectory(string path);

        /// <summary>
        /// Deletes the specified file.
        /// </summary>
        /// <param name="path">The file path.</param>
        void DeleteFile(string path);

        /// <summary>
        /// Moves a file to a new location.
        /// </summary>
        /// <param name="sourcePath">The source file path.</param>
        /// <param name="destinationPath">The destination file path.</param>
        void MoveFile(string sourcePath, string destinationPath);
    }
}