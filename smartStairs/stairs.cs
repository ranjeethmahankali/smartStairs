using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System.Diagnostics;

namespace smartStairs
{
    //the code restrictions
    public static class code
    {
        //in the future this class could lookup these standards some place online
        //and return them, without affecting the rest of the logic
        //these are in millimeters
        public static double MIN_RISER = 4;
        public static double MAX_RISER = 8;
        public static double RISER_DEFAULT = 6;

        public static double MIN_WIDTH = 36;
        public static double MAX_WIDTH = 400;
        public static double WIDTH_DEFAULT = 40;

        public static double MIN_TREAD = 11;
        public static double MAX_TREAD = 60;
        public static double TREAD_DEFAULT = 12;

        public static double MIN_RAIL_HEIGHT = 32;
        public static double MAX_RAIL_HEIGHT = 56;
        public static double RAIL_HEIGHT_DEFAULT = 44;

        //these are derived quantities - not defined or looked up as standards
        public static double MAX_SLOPE = MAX_RISER / MIN_TREAD;
        public static double MIN_SLOPE = MIN_RISER / MAX_TREAD;
    }

    //datatype for run
    class Run
    {
        //properties of a run
        public Point3d startPt;
        public Point3d endPt;
        public double Width;
        public double RailHeight;
        public bool leftRail;
        public bool rightRail;
        //this is a unit vector in the direction of the stringer
        public Vector3d StringerDirection
        {
            get
            {
                Vector3d stringer = new Vector3d(this.endPt - this.startPt);
                stringer.Unitize();
                return stringer;
            }
        }
        //this is the unit vector in the direction of the run ut horizontal - think plan view
        //parallel to your foot as you climb
        public Vector3d RunDirection
        {
            get
            {
                Point3d pt = this.endPt;
                pt.Z = this.startPt.Z;
                Vector3d run = new Vector3d(pt - this.startPt);
                run.Unitize();
                return run;
            }
        }
        //this property is a unit vector pointing in the direction of the tread
        //perpendicular to your foot as you climb
        public Vector3d TreadDirection
        {
            get
            {
                Vector3d treadDir;
                treadDir = Vector3d.CrossProduct(this.StringerDirection, Vector3d.ZAxis);
                treadDir.Unitize();
                return treadDir;
            }
        }
        //This is the direction of the riser - along z +ve or -ve based on ascent or descent
        public Vector3d RiserDirection
        {
            get
            {
                Point3d pt = this.endPt;
                pt.X = this.startPt.X;
                pt.Y = this.startPt.Y;

                Vector3d riser = new Vector3d(pt - this.startPt);
                riser.Unitize();
                return riser;
            }
        }
        //this is the bottom edge of the run
        public Line BottomEdge
        {
            get
            {
                Vector3d treadDir = this.TreadDirection*(this.Width/2);
                return new Line(Point3d.Add(this.startPt, treadDir), Point3d.Subtract(this.startPt, treadDir));
            }
            private set { }
        }
        //this is the top edge of the run
        public Line TopEdge
        {
            get
            {
                Vector3d treadDir = this.TreadDirection * (this.Width / 2);
                return new Line(Point3d.Add(this.endPt, treadDir), Point3d.Subtract(this.endPt, treadDir));
            }
            private set { }
        }
        //this is one of the side edges
        public Line SideEdge1
        {
            get
            {
                Vector3d treadDir = this.TreadDirection * (this.Width / 2);
                return new Line(Point3d.Add(this.startPt, treadDir), Point3d.Add(this.endPt, treadDir));
            }
            private set { }
        }
        //this is the other side edge
        public Line SideEdge2
        {
            get
            {
                Vector3d treadDir = this.TreadDirection * (this.Width / 2);
                return new Line(Point3d.Subtract(this.startPt, treadDir), Point3d.Subtract(this.endPt, treadDir));
            }
            private set { }
        }

        //this property is the riser height
        public double RiserDim
        {
            get
            {
                return this.VerticalDistance / this.NumSteps;
            }
        }
        //tread size
        public double TreadDim
        {
            get
            {
                return this.HorizontalDistance / this.NumSteps;
            }
        }
        //number of steps
        public int NumSteps
        {
            get
            {
                return Math.Max((int)Math.Floor(this.HorizontalDistance / code.MIN_TREAD), 1);
            }
        }
        //height difference covered in this run
        public double VerticalDistance
        {
            get
            {
                return Math.Abs(this.endPt.Z - this.startPt.Z);
            }
            private set { }
        }
        //horizontal distance covered in thsi run
        public double HorizontalDistance
        {
            get
            {
                Point3d pt = this.endPt;
                pt.Z = this.startPt.Z;
                //distance to be covered horizontally
                return this.startPt.DistanceTo(pt);
            }
            private set { }
        }
        //returns true if the slope of this run lies within the code limits
        public bool IsValid
        {
            get
            {
                double slope = this.VerticalDistance / this.HorizontalDistance;
                bool slopeIsValid = slope <= code.MAX_SLOPE && slope >= code.MIN_SLOPE;
                bool sizeOK = this.NumSteps > 0;
                return (slopeIsValid && sizeOK);
            }
        }

        //constructor
        public Run(Point3d pt1, Point3d pt2, double runWidth)
        {
            this.startPt = pt1;
            //now making sure the length is not too small
            Vector3d runDir = new Vector3d(pt2 - pt1);
            runDir.Unitize();
            Point3d minDistPt = pt1 + (runDir*code.MIN_TREAD);
            this.endPt = (pt1.DistanceTo(pt2) < code.MIN_TREAD) ? minDistPt : pt2;
            //getting width
            this.Width = runWidth;
            this.RailHeight = code.RAIL_HEIGHT_DEFAULT;
            this.leftRail = true;
            this.rightRail = true;
        }

        public List<Line> getFlatLines()
        {
            List<Line> lines = new List<Line>();
            lines.Add(this.BottomEdge);
            lines.Add(this.TopEdge);
            lines.Add(this.SideEdge1);
            lines.Add(this.SideEdge2);

            for(int i = 1; i < this.NumSteps; i++)
            {
                Vector3d move = this.RunDirection * this.TreadDim * i;
                Line l = new Line(this.BottomEdge.From + move, this.BottomEdge.To + move);
                lines.Add(l);
            }

            //Debug.WriteLine(this.StringerDirection, "Stringer");
            //Debug.WriteLine(this.Tre.To, "To");
            return lines;
        }

        //returns rails as a list of lines depending on the include parameters passed
        public List<Line> getRails()
        {
            List<Line> railSet = new List<Line>();
            Point3d railStart, railEnd;

            railStart = this.BottomEdge.From + (this.RiserDirection*(this.RiserDim/2));
            railStart += Vector3d.ZAxis* this.RailHeight;
            railEnd = this.TopEdge.From + (this.RiserDirection * (this.RiserDim / 2));
            railEnd += Vector3d.ZAxis * this.RailHeight;
            Line rightRail = new Line(railStart, railEnd);

            railStart = this.BottomEdge.To + (this.RiserDirection * (this.RiserDim / 2));
            railStart += Vector3d.ZAxis * this.RailHeight;
            railEnd = this.TopEdge.To + (this.RiserDirection * (this.RiserDim / 2));
            railEnd += Vector3d.ZAxis * this.RailHeight;
            Line leftRail = new Line(railStart, railEnd);

            if (this.leftRail) { railSet.Add(leftRail); }
            if (this.rightRail) { railSet.Add(rightRail); }

            return railSet;
        }

        //returns balusters as a list of lines
        public List<Line> getBalusters()
        {
            List<Line> balusters = new List<Line>();

            //starting points of the balusters
            Point3d left = this.BottomEdge.To + (this.RiserDirection * (this.RiserDim / 2));
            Point3d right = this.BottomEdge.From + (this.RiserDirection * (this.RiserDim / 2));
            Vector3d halfStepVec = (this.RiserDirection * (this.RiserDim / 2)) + (this.RunDirection*(this.TreadDim/2));
            for (int i = 0; i < this.NumSteps; i++)
            {
                Point3d leftBase = left + (((2 * i) + 1) * halfStepVec);
                Point3d rightBase = right + (((2 * i) + 1) * halfStepVec);

                if (this.leftRail) { balusters.Add(new Line(leftBase, leftBase + (Vector3d.ZAxis * this.RailHeight))); }
                if (this.rightRail) { balusters.Add(new Line(rightBase, rightBase + (Vector3d.ZAxis * this.RailHeight))); }
            }

            return balusters;
        }

        //returns the stepped surface of the stairs
        public Surface getStepSurface()
        {
            List<Point3d> ptList = new List<Point3d>();
            //moving the starting point to the edge and adding it to the list
            Point3d curPt = this.startPt - (this.TreadDirection*(this.Width/2));
            ptList.Add(curPt);
            for(int i = 0; i < this.NumSteps; i++)
            {
                curPt += this.RiserDirection * this.RiserDim;
                ptList.Add(curPt);
                curPt += this.RunDirection * this.TreadDim;
                ptList.Add(curPt);
            }

            Curve profile = Curve.CreateInterpolatedCurve(ptList, 1);
            Surface steps = Surface.CreateExtrusion(profile, (this.TreadDirection*this.Width));

            return steps;
        }
    }

    //datatype or a landing
    class Landing
    {
        //properties of a landing
        public Run bottomRun, topRun;
        //this is the angle by which the climber turns at this landing from run to run
        public double Alpha
        {
            get
            {
                return Vector3d.VectorAngle(this.bottomRun.RunDirection, this.topRun.RunDirection);
            }
        }
        //this being false means there is something wrong with this landing and it shouldn't be rendered
        public bool IsValid;
        //returns the bottom and top edges of this landing
        //bottom and top in the w.r.t to the progresion of the stairs.
        public Line BottomEdge { get { return this.bottomRun.TopEdge; } }
        public Line TopEdge { get { return this.topRun.BottomEdge; } }
        //the value of this string is 'from' or 'to' whichever is the nearest
        public string NearestSide
        {
            get
            {
                double df = this.BottomEdge.From.DistanceTo(this.TopEdge.From);
                double dt = this.BottomEdge.To.DistanceTo(this.TopEdge.To);
                if (df > dt) { return "to"; }
                else { return "from"; }//defaulting to from even when it is equal.
            }
        }
        //The Nearest side edge as a line
        //if the edges are equal then this returns the edge on the from side - right
        public Line NearEdge
        {
            get
            {
                if(NearestSide == "from") { return new Line(this.BottomEdge.From, this.TopEdge.From); }
                else { return new Line(this.BottomEdge.To, this.TopEdge.To); }
            }
        }
        //if the edges are equal then this returns the edge on the from side - right
        public Line FarEdge;
        //this is the Right side rail edge for the landing
        public Curve RightRailEdge;
        public Curve LeftRailEdge;
        //this is the surface of the landing
        public Brep LandingSurface
        {
            get
            {
                if(this.Alpha == 0){return this.getSurfaceTypeA();}
                else if(this.Alpha <= (Math.PI / 2)) { return this.getSurfaceTypeB(); }
                else{return this.getSurfaceTypeC();}
            }
        }

        //constructor
        public Landing(Run r1, Run r2)
        {
            //this is the angle between the runs
            this.bottomRun = r1;
            this.topRun = r2;
            Vector3d tVecBottom, tVecTop;
            if(this.NearestSide == "from")
            {
                tVecBottom = new Vector3d(this.BottomEdge.To - this.BottomEdge.From);
                tVecTop = new Vector3d(this.TopEdge.To - this.TopEdge.From);
            }
            else//this means the nearest side is the "To" side.
            {
                tVecBottom = new Vector3d(this.BottomEdge.From - this.BottomEdge.To);
                tVecTop = new Vector3d(this.TopEdge.From - this.TopEdge.To);
            }

            this.IsValid = this.Validate(tVecBottom, tVecTop);
        }

        //this returns if the angles of treads with the nearest side are greater than 90
        public bool Validate(Vector3d bottomT, Vector3d topT)
        {
            //dealing with a special case first
            if(this.Alpha == 0)
            {
                Vector3d joinVec = new Vector3d(this.topRun.startPt - this.bottomRun.endPt);
                return (this.bottomRun.RunDirection * joinVec > 0);    
            }

            //getting obvious stuff out of the way - landing has to be flat
            if(this.BottomEdge.From.Z != this.TopEdge.From.Z) { return false; }
            if(this.BottomEdge.To.Z != this.TopEdge.To.Z) { return false; }

            //now making sure the landings are not at a weird angle
            Vector3d nvb = new Vector3d(this.NearEdge.To - this.NearEdge.From);
            Vector3d nvt = new Vector3d(this.NearEdge.From - this.NearEdge.To);

            double angB = Vector3d.VectorAngle(nvb, bottomT);
            double angT = Vector3d.VectorAngle(nvt, topT);

            if(angT < (Math.PI/2) || angB < (Math.PI/2)) { return false; }

            double theta1 = this.SpecialAngle(bottomT, nvb, this.bottomRun.RunDirection);
            double theta2 = this.SpecialAngle(topT, nvt, -this.topRun.RunDirection);
            
            //this means the runs are turning more than they should
            if (theta1 + theta2 > (2 * Math.PI)) { return false; }

            //nothing went wrong and we made it till here so return true
            return true;
        }
        //this is a special way of calculating angle between a and b that is affected by vector m.
        //this is for use inside the validate function
        private double SpecialAngle(Vector3d a , Vector3d b, Vector3d m)
        {
            double theta;
            double a0 = Vector3d.VectorAngle(m, b);
            if(a0 > (Math.PI/2))
            {
                theta = a0 + Vector3d.VectorAngle(a, m);
            }else
            {
                theta = Vector3d.VectorAngle(a, b);
            }

            return theta;
        }

        //this is for when the runs are parallel
        private Brep getSurfaceTypeA()
        {
            //making decisions based on the lateral displacement
            Point3d corner1, corner2;
            Vector3d xVec;
            Vector3d yVec = this.bottomRun.RunDirection;
            Vector3d diagonal;
            string startingAt;
            if(this.BottomEdge.From.DistanceTo(this.TopEdge.To) >= this.BottomEdge.To.DistanceTo(this.TopEdge.From))
            {
                corner1 = this.BottomEdge.From;
                corner2 = this.TopEdge.To;
                xVec = new Vector3d(this.BottomEdge.To - this.BottomEdge.From);
                diagonal = new Vector3d(this.TopEdge.To - this.BottomEdge.From);
                startingAt = "from";
            }
            else
            {
                corner1 = this.BottomEdge.To;
                corner2 = this.TopEdge.From;
                xVec = new Vector3d(this.BottomEdge.From - this.BottomEdge.To);
                diagonal = new Vector3d(this.TopEdge.From - this.BottomEdge.To);
                startingAt = "to";
            }
            yVec.Unitize();
            xVec.Unitize();
            //now making the surface
            List<Point3d> railPts1 = new List<Point3d>();
            List<Point3d> railPts2 = new List<Point3d>();
            List<Point3d> ptList = new List<Point3d>();
            ptList.Add(corner1); railPts1.Add(corner1);
            double distX = diagonal*xVec;
            double distY = this.BottomEdge.DistanceTo(this.TopEdge.From, false);
            Point3d pt = corner1 + (yVec*distY);
            ptList.Add(pt); railPts1.Add(pt); railPts1.Add(pt + xVec*(distX - this.topRun.Width));
            pt += xVec*distX;
            ptList.Add(pt); railPts2.Add(pt);
            pt += distY*(-yVec);
            ptList.Add(pt); railPts2.Add(pt); railPts2.Add(pt -xVec*(distX - this.bottomRun.Width));
            ptList.Add(corner1);

            Curve edge = Curve.CreateInterpolatedCurve(ptList, 1);
            edge.MakeClosed(0);
            Curve railEdge1 = Curve.CreateInterpolatedCurve(railPts1, 1);
            Curve railEdge2 = Curve.CreateInterpolatedCurve(railPts2, 1);

            this.RightRailEdge = (startingAt == "from") ? railEdge1 : railEdge2;
            this.LeftRailEdge = (startingAt == "to") ? railEdge1 : railEdge2;

            Brep[] breps = Brep.CreatePlanarBreps(edge);

            return breps[0];
        }
        //this calculates the surface when the alpha angle is less than 90
        private Brep getSurfaceTypeB()
        {
            Line fromEdgeBottom, fromEdgeTop, toEdgeBottom, toEdgeTop;
            fromEdgeBottom = new Line(this.BottomEdge.From, this.BottomEdge.From + this.bottomRun.RunDirection);
            fromEdgeTop = new Line(this.TopEdge.From, this.TopEdge.From - this.topRun.RunDirection);
            toEdgeBottom = new Line(this.BottomEdge.To, this.BottomEdge.To+ this.bottomRun.RunDirection);
            toEdgeTop = new Line(this.TopEdge.To, this.TopEdge.To - this.topRun.RunDirection);

            Point3d fromIntersection, toIntersection;//the intersection points on both sides
            if ((!LineIntersection(fromEdgeBottom,fromEdgeTop, out fromIntersection))
                || (!LineIntersection(toEdgeBottom, toEdgeTop, out toIntersection)))
            {
                Brep empty = new Brep();
                //Debug.WriteLine("I am here.", "No Intersections");
                return empty;//exiting and returning a dummy brep
            }

            List<Point3d> fromRailpts = new List<Point3d>();
            List<Point3d> toRailpts = new List<Point3d>();
            List<Point3d> ptList = new List<Point3d>();
            ptList.Add(this.BottomEdge.From); fromRailpts.Add(this.BottomEdge.From);
            ptList.Add(fromIntersection); fromRailpts.Add(fromIntersection);
            ptList.Add(this.TopEdge.From); fromRailpts.Add(this.TopEdge.From);
            ptList.Add(this.TopEdge.To); toRailpts.Add(this.TopEdge.To);
            ptList.Add(toIntersection); toRailpts.Add(toIntersection);
            ptList.Add(this.BottomEdge.To); toRailpts.Add(this.BottomEdge.To);
            ptList.Add(this.BottomEdge.From);

            Curve landingEdge = Curve.CreateInterpolatedCurve(ptList, 1);
            landingEdge.MakeClosed(0);
            this.RightRailEdge = Curve.CreateInterpolatedCurve(fromRailpts, 1);
            this.LeftRailEdge = Curve.CreateInterpolatedCurve(toRailpts, 1);

            Brep[] breps = Brep.CreatePlanarBreps(landingEdge);
            return breps[0];
        }
        //this calculates the surface when the alpha angle is greater than 90
        private Brep getSurfaceTypeC()
        {
            Vector3d vec1, vec2;
            Vector3d vecMid = this.bottomRun.RunDirection + this.topRun.RunDirection;
            if(vecMid.Length == 0) { vecMid = this.bottomRun.TreadDirection * (this.bottomRun.TreadDirection * this.NearEdge.UnitTangent); }
            if (vecMid.Length == 0) { vecMid = this.NearEdge.UnitTangent; }
            Vector3d offsetDir = this.bottomRun.RunDirection - this.topRun.RunDirection;
            vecMid.Unitize(); offsetDir.Unitize();
            Point3d inner1, inner2, outer1, outer2, outerMiddle;
            if (this.NearEdge.UnitTangent * this.bottomRun.RunDirection >= 0)
            {
                vec1 = -this.topRun.RunDirection;
                vec2 = this.bottomRun.RunDirection;
                vecMid = -vecMid;
                if (this.NearestSide == "from")
                {
                    inner1 = this.TopEdge.From;
                    outer1 = this.TopEdge.To;
                    inner2 = this.BottomEdge.From;
                    outer2 = this.BottomEdge.To;
                }
                else
                {
                    inner1 = this.TopEdge.To;
                    outer1 = this.TopEdge.From;
                    inner2 = this.BottomEdge.To;
                    outer2 = this.BottomEdge.From;
                }
            }            
            else
            {
                vec1 = this.bottomRun.RunDirection;
                vec2 = -this.topRun.RunDirection;
                if (this.NearestSide == "from")
                {
                    inner1 = this.BottomEdge.From;
                    outer1 = this.BottomEdge.To;
                    inner2 = this.TopEdge.From;
                    outer2 = this.TopEdge.To;
                }
                else
                {
                    inner1 = this.BottomEdge.To;
                    outer1 = this.BottomEdge.From;
                    inner2 = this.TopEdge.To;
                    outer2 = this.TopEdge.From;
                }
            }
            outerMiddle = inner1 + offsetDir*(this.bottomRun.Width);

            List<Point3d> ptList = new List<Point3d>();
            List<Point3d> railPtsInner = new List<Point3d>();
            List<Point3d> railPtsOuter = new List<Point3d>();

            ptList.Add(inner1);railPtsInner.Add(inner1);
            Point3d pt, pt1, pt2;
            if(!LineIntersection(new Line(inner1, inner1 + vecMid), new Line(inner2, inner2+vec2), out pt))
            {
                Rhino.RhinoApp.WriteLine("Landing Creation Failed.");
                return new Brep();
            }
            ptList.Add(pt); railPtsInner.Add(pt);
            ptList.Add(inner2); railPtsInner.Add(inner2);
            ptList.Add(outer2); railPtsOuter.Add(outer2);
            if (!LineIntersection(new Line(outer2, outer2 + vec2), new Line(outerMiddle, outerMiddle+vecMid), out pt1))
            {
                Rhino.RhinoApp.WriteLine("Landing Creation Failed.");
                return new Brep();
            }
            ptList.Add(pt1); railPtsOuter.Add(pt1);
            if (!LineIntersection(new Line(outerMiddle, outerMiddle - vecMid), new Line(outer1, outer1+vec1),out pt2))
            {
                Rhino.RhinoApp.WriteLine("Landing Creation Failed.");
                return new Brep();
            }
            ptList.Add(pt2); railPtsOuter.Add(pt2);
            ptList.Add(outer1); railPtsOuter.Add(outer1);
            ptList.Add(inner1);

            Curve edge = Curve.CreateInterpolatedCurve(ptList, 1);
            edge.MakeClosed(0);
            Curve railOuter = Curve.CreateInterpolatedCurve(railPtsOuter, 1);
            Curve railInner = Curve.CreateInterpolatedCurve(railPtsInner, 1);

            this.RightRailEdge = (this.NearestSide == "from") ? railInner : railOuter;
            this.LeftRailEdge = (this.NearestSide == "to") ? railInner : railOuter;

            Brep[] breps = Brep.CreatePlanarBreps(edge);
            return breps[0];
        }

        //this returns the railing geometry of this landing as lines
        public List<Curve> getRailings(bool includeLeft, bool includeRight)
        {
            List<Curve> railing = new List<Curve>();
            double spacing = this.bottomRun.TreadDim;
            double height = this.bottomRun.RailHeight;
            double length;
            int num;
            Point3d[] divPts;
            if (includeLeft)
            {
                length = this.LeftRailEdge.GetLength();
                num = Math.Max((int)Math.Floor(length/spacing),1);
                this.LeftRailEdge.DivideByCount(num, true, out divPts);

                foreach(Point3d pt in divPts)
                {
                    Curve baluster = Curve.CreateInterpolatedCurve(new Point3d[] { pt, pt+(Vector3d.ZAxis*height)}, 1);
                    railing.Add(baluster);
                }

                Curve rail = this.LeftRailEdge.DuplicateCurve();
                rail.Translate(new Vector3d(0, 0, height));
                railing.Add(rail);
            }
            if (includeRight)
            {
                length = this.RightRailEdge.GetLength();
                num = Math.Max((int)Math.Floor(length / spacing), 1);
                this.RightRailEdge.DivideByCount(num, true, out divPts);

                foreach (Point3d pt in divPts)
                {
                    Curve baluster = Curve.CreateInterpolatedCurve(new Point3d[] { pt, pt + (Vector3d.ZAxis * height) }, 1);
                    railing.Add(baluster);
                }

                Curve rail = this.RightRailEdge.DuplicateCurve();
                rail.Translate(new Vector3d(0, 0, height));
                railing.Add(rail);
            }
            return railing;
        }
        
        //this function intersects two lines where the equations are known in vector form
        private static bool LineIntersection(Line l1,Line l2, out Point3d intPt)
        {
            double t1, t2;
            if(Intersection.LineLine(l1, l2, out t1, out t2))
            {
                double epsilon = 0.00001;
                double distance = l1.PointAt(t1).DistanceTo(l2.PointAt(t2));
                if(distance <= epsilon)
                {
                    intPt = l1.PointAt(t1);
                    return true;
                }
                else
                {
                    intPt = Point3d.Unset;
                    Debug.WriteLine("Distance is "+distance.ToString(), "Intersection Fail");
                    return false;
                }
            }
            else
            {
                intPt = Point3d.Unset;
                Debug.WriteLine("Two", "Intersection Fail");
                return false;
            }
        }
    }
}