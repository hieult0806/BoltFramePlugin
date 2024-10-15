using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using BoltFramePlugin.Models;
using Newtonsoft.Json;

namespace BoltFramePlugin.Services
{
    public class FileService
    {
        private readonly string _filePath;

        public FileService(string filePath)
        {
            _filePath = filePath;
        }

        public void SaveConfigurationData(string filePath, FrameConfigurationDataModel frameConfiguration)
        {
            string jsonData = JsonConvert.SerializeObject(frameConfiguration, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(_filePath, jsonData);
        }

        public FrameConfigurationDataModel LoadConfigurationData()
        {
            if (!File.Exists(_filePath))
            {
                return new FrameConfigurationDataModel();
            }

            string jsonData = File.ReadAllText(_filePath);
            return JsonConvert.DeserializeObject<FrameConfigurationDataModel>(jsonData);
        }
    }
}
