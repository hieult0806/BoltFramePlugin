using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using LoBIM.Filters;
using LoBIM.Services;
using Document = Autodesk.Revit.DB.Document;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace LoBIM.Features.Framing.EventHandlers
{

    public class CreateBeamPocketEventHandler : IExternalEventHandler
    {
        private IRevitService _service;
        public void SetParameters(IRevitService service)
        {
            _service = service;
        }

        public void Execute(UIApplication app)
        {
            var uiDoc = app.ActiveUIDocument;
            var sel = uiDoc.Selection;
            var doc = uiDoc.Document;
            Reference pickedWallRef = sel.PickObject(ObjectType.Element, new WallSelectionFilter(), "Select a wall.");
            Element wall = doc.GetElement(pickedWallRef);
            Reference pickedBeamRef = sel.PickObject(ObjectType.Element, new BeamSelectionFilter(), "Select a beam.");
            Element beam = doc.GetElement(pickedWallRef);
            CreateBeamPocket(app.ActiveUIDocument.Document, wall, beam);
        }

        public Solid GetIntersectionVolume(Document doc, Element beam, Element wall)
        {
            GeometryElement beamGeom = beam.get_Geometry(new Options());
            GeometryElement wallGeom = wall.get_Geometry(new Options());

            Solid beamSolid = null;
            Solid wallSolid = null;

            // Convert beam and wall geometry to solids
            foreach (GeometryObject geomObj in beamGeom)
            {
                if (geomObj is Solid solid && solid.Volume > 0)
                {
                    beamSolid = solid;
                    break;
                }
            }

            foreach (GeometryObject geomObj in wallGeom)
            {
                if (geomObj is Solid solid && solid.Volume > 0)
                {
                    wallSolid = solid;
                    break;
                }
            }

            // Check if we have valid solids
            if (beamSolid == null || wallSolid == null)
            {
                TaskDialog.Show("Error", "Couldn't get solids from beam or wall.");
                return null;
            }

            // Perform the intersection using Revit's Boolean operations
            Solid intersectionSolid = BooleanOperationsUtils.ExecuteBooleanOperation(wallSolid, beamSolid, BooleanOperationsType.Intersect);

            if (intersectionSolid == null || intersectionSolid.Volume <= 0)
            {
                TaskDialog.Show("Error", "No intersection found between the beam and the wall.");
                return null;
            }

            return intersectionSolid;
        }

        public FamilyInstance CreateVoidForBeamPocket(Document doc, Solid intersectionSolid)
        {
            FamilyInstance voidInstance = null;

            //using (Transaction tx = new Transaction(doc, "Create Beam Pocket Void"))
            //{
            //    tx.Start();

            //    // Create an in-place family for the void
            //    Family voidFamily = CreateInPlaceVoidFamily(doc, intersectionSolid);
            //    if (voidFamily == null)
            //    {
            //        TaskDialog.Show("Error", "Could not create in-place void family.");
            //        tx.RollBack();
            //        return null;
            //    }

            //    // Place the void family instance at the correct location
            //    FamilySymbol voidSymbol = doc.GetElement(voidFamily.GetFamilySymbolIds().First()) as FamilySymbol;
            //    if (voidSymbol != null)
            //    {
            //        // Ensure the family symbol is activated
            //        if (!voidSymbol.IsActive)
            //        {
            //            voidSymbol.Activate();
            //            doc.Regenerate();
            //        }

            //        // Get a location for placing the void
            //        XYZ placementPoint = GetPlacementLocation(intersectionSolid);
            //        voidInstance = doc.Create.NewFamilyInstance(placementPoint, voidSymbol, StructuralType.NonStructural);
            //    }

            //    tx.Commit();
            //}

            return voidInstance;
        }

        public XYZ GetPlacementLocation(Solid intersectionSolid)
        {
            if (intersectionSolid == null || intersectionSolid.Volume == 0)
            {
                throw new ArgumentException("Invalid intersection solid");
            }

            // Get the bounding box of the intersection solid
            BoundingBoxXYZ bbox = intersectionSolid.GetBoundingBox();

            // Calculate the center point of the bounding box
            XYZ centerPoint = (bbox.Min + bbox.Max) / 2.0;

            // Return the center point for placement
            return centerPoint;
        }

        public void CutWallWithVoid(Document doc, Element wall, FamilyInstance voidInstance)
        {
            using (Transaction tx = new Transaction(doc, "Cut Wall with Void"))
            {
                tx.Start();

                // Use the InstanceVoidCutUtils to cut the wall with the void instance
                if (InstanceVoidCutUtils.CanBeCutWithVoid(wall) && voidInstance != null)
                {
                    InstanceVoidCutUtils.AddInstanceVoidCut(doc, wall, voidInstance);
                    TaskDialog.Show("Success", "Wall has been cut with the void.");
                }
                else
                {
                    TaskDialog.Show("Error", "Failed to cut the wall with the void.");
                }

                tx.Commit();
            }
        }

        public void CreateBeamPocket(Document doc, Element beam, Element wall)
        {
            // Step 1: Find the intersection volume
            Solid intersectionSolid = GetIntersectionVolume(doc, beam, wall);

            if (intersectionSolid == null)
            {
                TaskDialog.Show("Error", "No intersection solid found.");
                return;
            }

            // Step 2: Create the void family instance based on the intersection
            FamilyInstance voidInstance = CreateVoidForBeamPocket(doc, intersectionSolid);

            if (voidInstance == null)
            {
                TaskDialog.Show("Error", "Failed to create void instance.");
                return;
            }

            // Step 3: Cut the wall with the void using InstanceVoidCutUtils
            CutWallWithVoid(doc, wall, voidInstance);
        }

        //Function to create an in-place void family from a solid(this is a simplified example)
        //public void CreateInPlaceVoidFamily(Document doc, Solid intersectionSolid)
        //{
        //    using (Transaction tx = new Transaction(doc, "Create In-Place Void"))
        //    {
        //        tx.Start();

        //        // Get the bounding box of the intersection solid
        //        BoundingBoxXYZ bbox = intersectionSolid.GetBoundingBox();
        //        XYZ minPoint = bbox.Min;
        //        XYZ maxPoint = bbox.Max;

        //        // Create the in-place family group in the Generic Model category
        //        FamilyItemFactory familyCreator = doc.FamilyCreate;
        //        ElementId genericModelId = new ElementId(BuiltInCategory.OST_GenericModel);

        //        // Start creating the in-place void family
        //        // Create an in-place family definition group
        //        FamilyInstance inPlaceVoidInstance = doc.Create.NewFamilyInstance( );

        //        if (inPlaceVoidInstance == null)
        //        {
        //            TaskDialog.Show("Error", "Could not create the in-place void family.");
        //            tx.RollBack();
        //            return;
        //        }

        //        // Generate the void extrusion based on the bounding box of the intersection solid
        //        CreateVoidExtrusion(doc, intersectionSolid, inPlaceVoidInstance);

        //        tx.Commit();
        //    }
        //}

        //public FamilyInstance CreateInPlaceVoid(Document doc, XYZ minPoint, XYZ maxPoint)
        //{
        //    // This method creates an in-place void
        //    using (Transaction tx = new Transaction(doc, "Create In-Place Void Family"))
        //    {
        //        tx.Start();

        //        // Start an in-place family for the void in the Generic Model category
        //        ElementId genericModelId = new ElementId(BuiltInCategory.OST_GenericModel);

        //        // Create the sketch plane for the void extrusion
        //        Plane voidPlane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, minPoint);
        //        SketchPlane sketchPlane = SketchPlane.Create(doc, voidPlane);

        //        // Create the profile for the void extrusion based on the bounding box
        //        CurveArrArray voidProfile = new CurveArrArray();
        //        voidProfile.Append(Line.CreateBound(new XYZ(minPoint.X, minPoint.Y, minPoint.Z), new XYZ(maxPoint.X, minPoint.Y, minPoint.Z)));
        //        voidProfile.Append(Line.CreateBound(new XYZ(maxPoint.X, minPoint.Y, minPoint.Z), new XYZ(maxPoint.X, maxPoint.Y, minPoint.Z)));
        //        voidProfile.Append(Line.CreateBound(new XYZ(maxPoint.X, maxPoint.Y, minPoint.Z), new XYZ(minPoint.X, maxPoint.Y, minPoint.Z)));
        //        voidProfile.Append(Line.CreateBound(new XYZ(minPoint.X, maxPoint.Y, minPoint.Z), new XYZ(minPoint.X, minPoint.Y, minPoint.Z)));

        //        // Create the void extrusion (true = void)
        //        FamilyInstance voidInstance = doc.FamilyCreate.NewExtrusion(true, voidProfile, sketchPlane, (maxPoint.Z - minPoint.Z));

        //        tx.Commit();

        //        return voidInstance;
        //    }
        //}

        //public void CreateVoidExtrusion(Document doc, Solid intersectionSolid, Group inPlaceVoidGroup)
        //{
        //    // Get the bounding box of the intersection solid
        //    BoundingBoxXYZ bbox = intersectionSolid.GetBoundingBox();
        //    XYZ minPoint = bbox.Min;
        //    XYZ maxPoint = bbox.Max;

        //    // Create a plane and a sketch plane for the void extrusion
        //    Plane voidPlane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, minPoint);
        //    SketchPlane sketchPlane = SketchPlane.Create(doc, voidPlane);

        //    // Define a rectangular profile based on the bounding box
        //    CurveArray profile = new CurveArray();
        //    profile.Append(Line.CreateBound(new XYZ(minPoint.X, minPoint.Y, minPoint.Z), new XYZ(maxPoint.X, minPoint.Y, minPoint.Z)));
        //    profile.Append(Line.CreateBound(new XYZ(maxPoint.X, minPoint.Y, minPoint.Z), new XYZ(maxPoint.X, maxPoint.Y, minPoint.Z)));
        //    profile.Append(Line.CreateBound(new XYZ(maxPoint.X, maxPoint.Y, minPoint.Z), new XYZ(minPoint.X, maxPoint.Y, minPoint.Z)));
        //    profile.Append(Line.CreateBound(new XYZ(minPoint.X, maxPoint.Y, minPoint.Z), new XYZ(minPoint.X, minPoint.Y, minPoint.Z)));

        //    // Create the void extrusion (true = void, false = solid)
        //    doc.FamilyCreate.NewExtrusion(true, profile, sketchPlane, maxPoint.Z - minPoint.Z);

        //    // Add the void extrusion to the in-place family group
        //    inPlaceVoidGroup.Append(doc.FamilyCreate.NewGroupInstance(profile));
        //}


        public string GetName()
        {
            return "Place Beam External Event";
        }
    }
}