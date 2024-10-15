using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BoltFramePlugin.Factories;
using BoltFramePlugin.FramingStrategies;
using BoltFramePlugin.Models;
using BoltFramePlugin.Services;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace BoltFramePlugin.EventHandlers
{
    internal class GenerateEventHandler : IExternalEventHandler
    {
        private IRevitService _service;
        private IFrameGenerateModel _frameGenerateModel;
        public void SetParameters(IRevitService service, IFrameGenerateModel model)
        {
            _service = service;
            _frameGenerateModel = model;
        }

        public void Execute(UIApplication app)
        {
            var framingStrategy = FramingStrategyFactory.GetFramingStrategy(_service, _frameGenerateModel);
            framingStrategy.StartGenerate();
        }

        

        //public void CreateHorizontalBeams(Wall selectedWall, FamilySymbol beamSymbol, Level beamLevel,
        //    double frameHeight, double frameWidth, double verticalOffset, int numberOfBeams, UIDocument uidoc)
        //{
        //    Document doc = uidoc.Document;

        //    // Get the wall's location curve (to use as a reference for the frame's position)
        //    LocationCurve wallLocationCurve = selectedWall.Location as LocationCurve;
        //    if (wallLocationCurve == null)
        //    {
        //        TaskDialog.Show("Error", "Unable to get wall location.");
        //        return;
        //    }

        //    // Define the bottom left and bottom right points for the horizontal beams
        //    XYZ startPoint = wallLocationCurve.Curve.GetEndPoint(0);
        //    XYZ bottomLeft = new XYZ(startPoint.X, startPoint.Y, startPoint.Z + verticalOffset);
        //    XYZ bottomRight = new XYZ(bottomLeft.X + frameWidth, bottomLeft.Y, bottomLeft.Z);

        //    // Calculate the vertical spacing between beams
        //    double verticalSpacing = frameHeight / (numberOfBeams - 1); // Dividing the frame height into equal segments for beams

        //    using (Transaction trans = new Transaction(doc, "Create Horizontal Beams"))
        //    {
        //        try
        //        {
        //            trans.Start();

        //            // Ensure the beam family symbol is active
        //            if (!beamSymbol.IsActive)
        //            {
        //                doc.Regenerate();
        //                beamSymbol.Activate();
        //                doc.Regenerate();
        //            }

        //            // Place horizontal beams at each spacing interval
        //            for (int i = 0; i < numberOfBeams; i++)
        //            {
        //                double currentHeight = bottomLeft.Z + i * verticalSpacing; // Calculate the height for each beam
        //                XYZ currentLeft = new XYZ(bottomLeft.X, bottomLeft.Y, currentHeight);
        //                XYZ currentRight = new XYZ(bottomRight.X, bottomRight.Y, currentHeight);

        //                // Place the beam between the current left and right points
        //                PlaceBeam(doc, beamSymbol, currentLeft, currentRight, beamLevel);
        //            }

        //            trans.Commit();
        //        }
        //        catch (Exception ex)
        //        {
        //            TaskDialog.Show("Error", $"Failed to create horizontal beams: {ex.Message}");
        //            trans.RollBack();
        //        }
        //    }
        //}

        //// Helper method to place a beam between two points
        //private void PlaceBeam(Document doc, FamilySymbol beamSymbol, XYZ startPoint, XYZ endPoint, Level beamLevel)
        //{
        //    // Create a line between the start and end points
        //    Line beamLine = Line.CreateBound(startPoint, endPoint);

        //    // Place the beam using the line and beam family symbol
        //    doc.Create.NewFamilyInstance(beamLine, beamSymbol, beamLevel, Autodesk.Revit.DB.Structure.StructuralType.Beam);
        //}

        public string GetName()
        {
            return "Place Beam External Event";
        }
    }
}