using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LoBIM.Services;

namespace LoBIM.Features.NBCReview.EventHandlers
{
    public class UpdateWallParameterEventHandler : IExternalEventHandler
    {
        private readonly ILoggingService _logger;
        private Wall? _wall;
        private string? _parameterName;
        private string? _parameterValue;

        public UpdateWallParameterEventHandler(ILoggingService logger)
        {
            _logger = logger;
        }

        public void SetParameters(Wall wall, string parameterName, string parameterValue)
        {
            _wall = wall;
            _parameterName = parameterName;
            _parameterValue = parameterValue;
        }

        public void Execute(UIApplication app)
        {
            if (_wall == null || string.IsNullOrEmpty(_parameterName) || _parameterValue == null)
            {
                _logger.LogWarning("UpdateWallParameterEventHandler: Missing required parameters");
                return;
            }

            try
            {
                Document doc = _wall.Document;

                using (Transaction trans = new Transaction(doc, $"Update {_parameterName}"))
                {
                    trans.Start();

                    // Try to find the parameter by name
                    Parameter? param = _wall.LookupParameter(_parameterName);

                    if (param == null)
                    {
                        // Parameter doesn't exist, try to create it as a shared parameter or project parameter
                        _logger.LogWarning($"Parameter '{_parameterName}' not found on wall {_wall.Id.Value}. Attempting to use Comments parameter instead.");

                        // Fallback to Comments parameter which is always available
                        param = _wall.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    }

                    if (param != null && !param.IsReadOnly)
                    {
                        param.Set(_parameterValue);
                        _logger.LogInformation($"Updated wall {_wall.Id.Value} parameter '{_parameterName}' to '{_parameterValue}'");
                    }
                    else
                    {
                        _logger.LogWarning($"Parameter '{_parameterName}' is read-only or not found on wall {_wall.Id.Value}");
                    }

                    trans.Commit();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating wall parameter: {ex.Message}", ex);
            }
        }

        public string GetName()
        {
            return "UpdateWallParameterEventHandler";
        }
    }
}
