using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace smartStairs
{
    [System.Runtime.InteropServices.Guid("33006f51-e5c0-4e90-9bf7-1cc41dd184df")]
    public class smartStairsCommand : Command
    {
        public smartStairsCommand()
        {
            this.width = new OptionDouble(code.WIDTH_DEFAULT, code.MIN_WIDTH, code.MAX_WIDTH);
            this.railHeight = new OptionDouble(code.RAIL_HEIGHT_DEFAULT, code.MIN_RAIL_HEIGHT, code.MAX_RAIL_HEIGHT);
            this.leftRail = new OptionToggle(true, "Off", "On");
            this.rightRail = new OptionToggle(true, "Off", "On");
            this.LandingToggle = new OptionToggle(true, "Off", "On");
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static smartStairsCommand Instance
        {
            get; private set;
        }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName
        {
            get { return "smartStairs"; }
        }

        //these are all the options for the command
        private OptionDouble width;
        private OptionDouble railHeight;
        private OptionToggle leftRail;
        private OptionToggle rightRail;
        private OptionToggle LandingToggle;
        private Run curRun;
        private Run prevRun;

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            int i = 0;
            double level = 0;
            while (true)
            {
                Plane pl = (i > 0) ? new Plane(new Point3d(0, 0, level), Vector3d.ZAxis) :new Plane();

                Result getResult;
                Point3d pt0 = Point3d.Unset;
                getResult = GetFirstPoint(ref pt0, pl, (i==0));
                //Debug.WriteLine(pt0, "Point");
                if (getResult != Result.Success)
                {
                    RhinoApp.WriteLine("Failed to get the first point. Ending Command.");
                    Debug.WriteLine(getResult, "Result");
                    return getResult;
                }

                Point3d pt1 = Point3d.Unset;
                getResult = GetSecondPoint(pt0, ref pt1, (i==0));
                //Debug.WriteLine(pt1,"Point");
                if (getResult != Result.Success)
                {
                    RhinoApp.WriteLine("Failed to get the second point. Run not created.");
                    Debug.WriteLine(getResult,"Result");
                    return getResult;
                }

                Point3d pt2 = Point3d.Unset;
                getResult = GetThirdPoint(pt0, pt1, ref pt2, (i == 0));
                if (getResult != Result.Success)
                {
                    RhinoApp.WriteLine("Failed to get the third point. Run Not created.");
                    return getResult;
                }

                curRun = new Run(pt0, pt2, this.width.CurrentValue);
                updateRunOptions(ref curRun);
                //adding the new run surface
                doc.Objects.AddSurface(curRun.getStepSurface());
                List<Guid> rail_ids = AddLines(ref doc, curRun.getRails());
                List<Guid> bal_ids = AddLines(ref doc, curRun.getBalusters());
                int groupNum = doc.Groups.Add();
                doc.Groups.AddToGroup(groupNum, rail_ids);
                doc.Groups.AddToGroup(groupNum, bal_ids);

                //adding landing if valid and user wants it
                if (i > 0 && this.LandingToggle.CurrentValue)
                {
                    Landing land = new Landing(prevRun, curRun);
                    if (land.IsValid)
                    {
                        doc.Objects.AddBrep(land.LandingSurface);
                        List<Curve> railing = land.getRailings(this.leftRail.CurrentValue, this.rightRail.CurrentValue);
                        foreach (Curve rail in railing)
                        {
                            doc.Objects.AddCurve(rail);
                        }
                    }
                    else { RhinoApp.WriteLine("The relationship between the runs is ambiguous, Creation of landing will be skipped."); }
                }

                i++;
                level = pt2.Z;
                //updating the run for the next time
                prevRun = curRun;
                doc.Views.Redraw();
            }
        }
        //this function gets the first point from the user
        private Result GetFirstPoint(ref Point3d pt, Plane pl, bool IsFirstRun)
        {
            using (GetPoint getPointAction = new GetPoint())
            {
                if (IsFirstRun)
                {
                    getPointAction.AddOptionDouble("Width", ref this.width);
                    getPointAction.AddOptionDouble("RailHeight", ref this.railHeight);
                    getPointAction.AddOptionToggle("LeftRail", ref this.leftRail);
                    getPointAction.AddOptionToggle("RightRail", ref this.rightRail);
                }
                if (!IsFirstRun) getPointAction.AddOptionToggle("Landing", ref this.LandingToggle);

                getPointAction.SetCommandPrompt("Begin making stairs. Select the starting point of the run");
                if (!IsFirstRun) { getPointAction.Constrain(pl, false); }

                while (true)
                {
                    GetResult get_rc = getPointAction.Get();
                    if (getPointAction.CommandResult() != Result.Success) { return getPointAction.CommandResult(); }
                    if (get_rc == GetResult.Point){pt = getPointAction.Point(); }
                    else if (get_rc == GetResult.Option){continue;}
                    break;
                }

                return Result.Success;
            }
        }
        //gets the second point from the user
        private Result GetSecondPoint(Point3d firstPt, ref Point3d pt, bool IsFirstRun)
        {
            Run myRun;
            using (GetPoint getPointAction = new GetPoint())
            {
                if (IsFirstRun)
                {
                    getPointAction.AddOptionDouble("Width", ref this.width);
                    getPointAction.AddOptionDouble("RailHeight", ref this.railHeight);
                    getPointAction.AddOptionToggle("LeftRail", ref this.leftRail);
                    getPointAction.AddOptionToggle("RightRail", ref this.rightRail);
                }
                if (!IsFirstRun) getPointAction.AddOptionToggle("Landing", ref this.LandingToggle);

                getPointAction.SetCommandPrompt("Select the ending point of the run (projected)");
                getPointAction.SetBasePoint(firstPt, true);

                Plane hPlane = new Plane(firstPt, Vector3d.ZAxis);
                getPointAction.Constrain(hPlane, false);
                getPointAction.DynamicDraw += (sender, e) =>
                {
                    myRun = new Run(firstPt, e.CurrentPoint, this.width.CurrentValue);
                    e.Display.DrawLines(myRun.getFlatLines(), Color.White);
                    if ((!IsFirstRun) && LandingToggle.CurrentValue)
                    {
                        Landing land = new Landing(this.prevRun, myRun);
                        if(land.IsValid)e.Display.DrawBrepWires(land.LandingSurface, Color.Blue);
                    }
                };

                while (true)
                {
                    GetResult get_rc = getPointAction.Get();
                    if (getPointAction.CommandResult() != Result.Success) { return getPointAction.CommandResult(); }
                    if (get_rc == GetResult.Point){pt = getPointAction.Point();}
                    else if (get_rc == GetResult.Option) { continue; }
                    break;
                }
                
                return Result.Success;
            }
        }
        // gets the third point from the user
        private Result GetThirdPoint(Point3d firstPt, Point3d secondPt, ref Point3d pt, bool IsFirstRun)
        {
            Run myRun;
            using (GetPoint getPointAction = new GetPoint())
            {
                if (IsFirstRun)
                {
                    getPointAction.AddOptionDouble("Width", ref this.width);
                    getPointAction.AddOptionDouble("RailHeight", ref this.railHeight);
                    getPointAction.AddOptionToggle("LeftRail", ref this.leftRail);
                    getPointAction.AddOptionToggle("RightRail", ref this.rightRail);
                }
                if (!IsFirstRun) getPointAction.AddOptionToggle("Landing", ref this.LandingToggle);

                getPointAction.SetCommandPrompt("Select the third point to determine the height of the run");
                getPointAction.SetBasePoint(secondPt, true);
                getPointAction.Constrain(secondPt, secondPt + Vector3d.ZAxis);

                getPointAction.DynamicDraw += (sender, e) =>
                {
                    myRun = new Run(firstPt, e.CurrentPoint, this.width.CurrentValue);
                    updateRunOptions(ref myRun);
                    Color drawColor = Color.White;
                    if (!myRun.IsValid)
                    {
                        double validVert = myRun.VerticalDistance;
                        double mySlope = myRun.VerticalDistance / myRun.HorizontalDistance;
                        if(mySlope < code.MIN_SLOPE)
                        {
                            validVert = myRun.HorizontalDistance * code.MIN_SLOPE;
                        }
                        else if(mySlope > code.MAX_SLOPE)
                        {
                            validVert = myRun.HorizontalDistance * code.MAX_SLOPE;
                        }

                        Point3d validEndPt = secondPt + (myRun.RiserDirection * validVert);
                        Run validRun = new Run(firstPt, validEndPt, this.width.CurrentValue);
                        updateRunOptions(ref validRun);
                        e.Display.DrawSurface(validRun.getStepSurface(), drawColor, 1);
                        drawColor = Color.Red;
                    }
                    e.Display.DrawSurface(myRun.getStepSurface(), drawColor, 2);
                    e.Display.DrawLines(myRun.getRails(), drawColor);
                    e.Display.DrawLines(myRun.getBalusters(), drawColor);
                    //drawing the landing if this is not the first run
                    if ((!IsFirstRun)&&LandingToggle.CurrentValue)
                    {
                        Landing land = new Landing(this.prevRun, myRun);
                        if (land.IsValid)
                        {
                            e.Display.DrawBrepWires(land.LandingSurface, Color.Blue);
                            List<Curve> railing = land.getRailings(this.leftRail.CurrentValue, this.rightRail.CurrentValue);
                            foreach(Curve rail in railing)
                            {
                                e.Display.DrawCurve(rail, Color.Blue);
                            }
                        }
                    }
                };

                while (true)
                {
                    GetResult get_rc = getPointAction.Get();
                    if (getPointAction.CommandResult() != Result.Success) { return getPointAction.CommandResult(); }
                    if (get_rc == GetResult.Point){pt = getPointAction.Point();}
                    else if (get_rc == GetResult.Option) { continue; }
                    break;
                }

                return Result.Success;
            }
        }
        //updates the given run to match with the current option values
        private void updateRunOptions(ref Run run)
        {
            run.Width = this.width.CurrentValue;
            run.RailHeight = this.railHeight.CurrentValue;
            run.leftRail = this.leftRail.CurrentValue;
            run.rightRail = this.rightRail.CurrentValue;
        }
        //adds these lines to the document and returns the list of guids
        private List<Guid> AddLines(ref RhinoDoc doc, List<Line> lines)
        {
            List<Guid> ids = new List<Guid>();
            foreach(Line line in lines)
            {
                ids.Add(doc.Objects.AddLine(line));
            }

            return ids;
        }
    }
}