/*
 * This file is part of the Buildings and Habitats object Model (BHoM)
 * Copyright (c) 2015 - 2023, the respective contributors. All rights reserved.
 *
 * Each contributor holds copyright over their respective contributions.
 * The project versioning (Git) records all such contribution source information.
 *                                           
 *                                                                              
 * The BHoM is free software: you can redistribute it and/or modify         
 * it under the terms of the GNU Lesser General Public License as published by  
 * the Free Software Foundation, either version 3.0 of the License, or          
 * (at your option) any later version.                                          
 *                                                                              
 * The BHoM is distributed in the hope that it will be useful,              
 * but WITHOUT ANY WARRANTY; without even the implied warranty of               
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the                 
 * GNU Lesser General Public License for more details.                          
 *                                                                            
 * You should have received a copy of the GNU Lesser General Public License     
 * along with this code. If not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.      
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BH.oM.Geometry;
using BH.oM.Base.Attributes;
using System.ComponentModel;
using BH.Engine.Reflection;
using BH.oM.Environment.Elements;
using BH.Engine.Environment;
using BH.Engine.Geometry;

using BH.oM.Geometry.CoordinateSystem;
using System.Diagnostics;

namespace BH.Adapter.DIALux
{
    public static partial class Convert
    {
        [Description("Convert a BHoM Environment Opening into a DialUX Furnishing")]
        [Input("opening", "A BHoM Environment Opening to convert")]
        [Input("hostPanel", "The BHoM Environment Panel which hosts this Opening to convert to a DIALux furnishing")]
        [Output("furnishing", "A DialUX opening represented as a 'furnishing'")]
        public static Furnishing ToDIALux(this Opening opening, Panel hostPanel)
        {
            Furnishing furnishing = new Furnishing();
            furnishing.Type = opening.Type.ToDIALux();
            furnishing.Reference = "";
            furnishing.RotationX = 0;
            furnishing.RotationY = 0;
            furnishing.RotationZ = 0;

            Point centre = opening.Polyline().Centroid();
            centre.Z -= (opening.Polyline().Height() / 2);

            double bottomHost = hostPanel.Polyline().ControlPoints.Select(x => x.Z).Min();
            centre.Z -= bottomHost;

            furnishing.Position = centre;
            furnishing.Height = Math.Round(opening.Polyline().Height(), 3);
            furnishing.Width = Math.Round(opening.Polyline().Width(), 3);
            furnishing.Depth = 0;

            return furnishing;
        }

        public static Opening FromDialUXOpening(this List<string> furnishing, List<Panel> panelsAsSpace)
        {
            Opening opening = new Opening();

            Point centre = furnishing[3].FromDialUXPoint();

            string[] size = furnishing[4].Split('=')[1].Split(' ');
            double width = System.Convert.ToDouble(size[0]);
            double height = System.Convert.ToDouble(size[1]);

            centre.Z += (height / 2);

            double minZ = panelsAsSpace.Select(x => x.Polyline().ControlPoints.Select(y => y.Z).Min()).Min();
            centre.Z += minZ;

            Panel host = panelsAsSpace.Where(x => x.IsContaining(centre)).FirstOrDefault();
            if (host == null)
            {
                BH.Engine.Base.Compute.RecordError("A suitable host panel for opening reference " + furnishing[1] + " - opening may not have been correctly pulled.");
                return opening;
            }

            double bottomHost = host.Polyline().ControlPoints.Select(x => x.Z).Min();
            if(bottomHost != minZ)
                centre.Z += bottomHost;

            Point panelBottomRightReference = host.BottomRight(panelsAsSpace);
            Point panelBottomLeftReference = host.BottomLeft(panelsAsSpace);
            Point panelTopRightReference = host.TopRight(panelsAsSpace);

            Vector xVector = panelBottomLeftReference - panelBottomRightReference;
            xVector.Z = 0;
            Vector yVector = panelTopRightReference - panelBottomRightReference;

            Point worldOrigin = new Point { X = 0, Y = 0, Z = 0 };
            Cartesian worldCartesian = BH.Engine.Geometry.Create.CartesianCoordinateSystem(worldOrigin, Vector.XAxis, Vector.YAxis);
            Cartesian localCartesian = BH.Engine.Geometry.Create.CartesianCoordinateSystem(panelBottomRightReference, xVector, yVector);

            Point centreTransformed = centre.Orient(localCartesian, worldCartesian);

            List<Point> openingPts = new List<Point>();
            openingPts.Add(new Point { X = centreTransformed.X - (width / 2), Y = centreTransformed.Y - (height / 2), Z = centreTransformed.Z });
            openingPts.Add(new Point { X = centreTransformed.X - (width / 2), Y = centreTransformed.Y + (height / 2), Z = centreTransformed.Z });
            openingPts.Add(new Point { X = centreTransformed.X + (width / 2), Y = centreTransformed.Y + (height / 2), Z = centreTransformed.Z });
            openingPts.Add(new Point { X = centreTransformed.X + (width / 2), Y = centreTransformed.Y - (height / 2), Z = centreTransformed.Z });
            openingPts.Add(openingPts.First());

            Polyline openingCurve = new Polyline { ControlPoints = openingPts };

            openingCurve = openingCurve.Orient(worldCartesian, localCartesian);

            opening.Edges = openingCurve.ToEdges();
            opening.Type = furnishing[0].Split('=')[1].FromDialUXOpeningType();

            host.Openings.Add(opening);
            return opening;
        }
    }
}


