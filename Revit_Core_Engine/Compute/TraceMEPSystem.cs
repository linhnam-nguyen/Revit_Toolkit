/*
 * This file is part of the Buildings and Habitats object Model (BHoM)
 * Copyright (c) 2015 - 2025, the respective contributors. All rights reserved.
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

using Autodesk.Revit.DB;
using BH.Engine.Adapters.Revit;
using BH.oM.Adapters.Revit.Settings;
using BH.oM.Base;
using BH.oM.Base.Attributes;
using BH.oM.Data.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace BH.Revit.Engine.Core
{
    public static partial class Compute
    {
        /***************************************************/
        /****             Interface methods             ****/
        /***************************************************/

        [Description("----------Looks for a Revit element that geometrically contains the given BHoM object.")]
        [Input("bHoMObject", "----------BHoM object to find the containing element for.")]
        [Input("document", "----------Revit document to be searched for the containing element.")]
        [Input("settings", "----------Revit adapter settings to be used while performing the query.")]
        [Output("host", "----------First Revit element that contains the input BHoM object.")]
        public static Element ITraceMEPSystem(this IBHoMObject bHoMObject, Document document, RevitSettings settings = null)
        {
            if (bHoMObject == null)
            {
                BH.Engine.Base.Compute.RecordError("Host element could not be found for a null BHoM object.");
                return null;
            }

            return TraceMEPSystem(bHoMObject as dynamic, document, settings);
        }


        /***************************************************/
        /****              Public methods               ****/
        /***************************************************/

        [Description("Looks for a Revit element that geometrically contains the given builders work Opening.")]
        [Input("opening", "Builders work Opening to find the containing element for.")]
        [Input("document", "Revit document to be searched for the containing element.")]
        [Input("settings", "Revit adapter settings to be used while performing the query.")]
        [Output("host", "First Revit element that contains the input builders work Opening.")]
        public static Element TraceMEPSystem(this BH.oM.Architecture.BuildersWork.Opening opening, Document document, RevitSettings settings = null)
        {
            BuiltInCategory[] hostCategories = new BuiltInCategory[] { BuiltInCategory.OST_Floors, BuiltInCategory.OST_Walls, BuiltInCategory.OST_Roofs };
            return opening.FindHost(document, hostCategories, settings);
        }

        /***************************************************/

        public static Tree<Element> TraceMEPSystem(this Element startElement, bool downStream = false, bool includeMEPCurveAndFitting = false, HashSet<ElementId> visited = null)
        {
            visited = (visited != null) ? visited : new HashSet<ElementId>();

            if (startElement == null || visited.Contains(startElement.Id))
                return null;

            visited.Add(startElement.Id);

            var systemTree = new Tree<Element>
            {
                Name = startElement.Id.ToString(),
                Value = startElement
            };

            List<Connector> connectors = startElement.Connectors();
            if (connectors == null)
                return systemTree;

            // Try downstream first
            var nextConnectors = NextConnectedConnectors(connectors, FlowDirectionType.Out);

            // If none and downstream mode, fallback to upstream
            if (!nextConnectors.Any() && downStream)
                nextConnectors = NextConnectedConnectors(connectors, FlowDirectionType.In);

            foreach (Connector conn in nextConnectors)
            {
                Connector linked = conn.GetConnectedConnector();
                if (linked == null)
                    continue;

                Element child = linked.Owner as Element;

                if (child == null || visited.Contains(child.Id))
                    continue;

                bool isCurveOrFitting = child is MEPCurve || IsFitting(child);

                if (includeMEPCurveAndFitting || !isCurveOrFitting)
                {
                    var childTree = TraceMEPSystem(child, downStream, includeMEPCurveAndFitting, visited);
                    if (childTree != null && childTree.Value != null)
                        systemTree.Children.Add(childTree.Name, childTree);
                }
            }

            return systemTree;
        }

        /***************************************************/

        public static ConnectorSet GetConnectors(this Element element)
        {
            if (element is MEPCurve curve)
                return curve.ConnectorManager?.Connectors;
            else if (element is FamilyInstance inst)
                return inst.MEPModel?.ConnectorManager?.Connectors;

            return null;
        }

        /***************************************************/

        private static List<Connector> NextConnectedConnectors(List<Connector> connectors, FlowDirectionType direction)
        {
            var result = new List<Connector>();

            foreach (Connector conn in connectors)
            {
                if (conn.Direction == direction || conn.Direction == FlowDirectionType.Bidirectional)
                {
                    var refs = conn.AllRefs;
                    foreach (Connector connected in refs)
                    {
                        if (!connected.Owner.Id.Equals(conn.Owner.Id)) // avoid self-loop
                            result.Add(connected);
                    }
                }
            }
            return result;
        }

        /***************************************************/

        private static readonly HashSet<BuiltInCategory> FittingCategories = new HashSet<BuiltInCategory>()
        {
            BuiltInCategory.OST_DuctFitting,
            BuiltInCategory.OST_PipeFitting,
            BuiltInCategory.OST_CableTrayFitting,
            BuiltInCategory.OST_ConduitFitting
        };

        /***************************************************/

        private static bool IsFitting(this Element element)
        {
            if (element?.Category == null)
                return false;

            return FittingCategories.Contains((BuiltInCategory)element.Category.Id.IntegerValue);
        }
    }
}




