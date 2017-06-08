﻿///////////////////////////////////////////////////////////////////////////////////////
//
//  TwainDirectOnTwain.SwordTask
//
//  This class follows the full lifecycle of a TWAIN Direct task, carrying its
//  inputs and supprot data, and returning the result.  The theory is we have
//  a one-stop shop for everything we want to do in a task, and functions don't
//  need any other context than what this class brings with it.
//
//  Compare this class to ApiCmd for a similiar concept in TwainDirectScanner.
//
//  The name SWORD (Scanning WithOut Requiring Drivers) was superceded by the
//  name TWAIN Direct.  However, we're doing TWAIN stuff in this assembly, and
//  the names are too close for comfort, which is why SWORD is still in use...
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    29-Jun-2014     Splitting up files...
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2014-2016 Kodak Alaris Inc.
//
//  Permission is hereby granted, free of charge, to any person obtaining a
//  copy of this software and associated documentation files (the "Software"),
//  to deal in the Software without restriction, including without limitation
//  the rights to use, copy, modify, merge, publish, distribute, sublicense,
//  and/or sell copies of the Software, and to permit persons to whom the
//  Software is furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
//  THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
//  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
//  DEALINGS IN THE SOFTWARE.
///////////////////////////////////////////////////////////////////////////////////////

// Helpers...
using System;
using TwainDirectSupport;

namespace TwainDirectOnTwain
{
    /// <summary>
    /// Deserialize a SWORD Task into container classes.  Cascade some values,
    /// like exception and vendor.  Otherwise this is mostly just a place
    /// to hold the data pulled from a JSON string.
    /// </summary>
    #region SwordTask

    /// <summary>
    /// This SWORD object wraps all of the JSON content...
    /// </summary>
    public sealed class SwordTask
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Init stuff...
        /// </summary>
        public SwordTask()
        {
            m_task = null;
            m_blSuccess = true;
            m_szException = null;
            m_szJsonExceptionKey = null;
            m_szSwordValue = null;
            m_szTwainValue = null;
            m_lResponseCharacterOffset = -1;
            m_szTaskReply = null;
            m_guidTwainDirect = new Guid("211a1e90-11e1-11e5-9493-1697f925ec7b");
        }

        /// <summary>
        /// Turn a JSON string into something we can deal with.  Specifically,
        /// the structure described in the SWORD documentation.  This structure
        /// takes on the topology of the request.  This means that the only
        /// elements we're going to find in it are the ones requested by the
        /// caller.
        /// 
        /// We have options at that point.  If the device is already set to
        /// the desired preset (such as factory default), then we only have
        /// to apply the task to it.  In this case the developer may opt to
        /// find the preset and send that down, then follow it with the rest
        /// of the contents within the task.
        /// 
        /// On the other paw it might be easier for some developers to merge
        /// the task with the relevant baseline settings and fire the whole
        /// thing over to the device.  Bearing in mind that they may need to
        /// some merging on the other side when they try to construct the
        /// metadata that goes with the image.
        /// </summary>
        /// <param name="a_szTask">task to process</param>
        /// <param name="a_guidScanner">this scanner's guid</param>
        /// <param name="a_swordtask">the object accompanying this task</param>
        /// <returns>true on success</returns>
        public static bool Deserialize(string a_szTask, Guid a_guidScanner, ref SwordTask a_swordtask)
        {
            int iAction;
            int iStream;
            int iSource;
            int iPixelFormat;
            int iAttribute;
            int iValue;
            bool blSuccess;
            string szSwordAction;
            string szSwordStream;
            string szSwordSource;
            string szSwordPixelFormat;
            string szSwordAttribute;
            string szSwordValue;
            long lResponseCharacterOffset;
            JsonLookup.EPROPERTYTYPE epropertytype;
            SwordAction swordaction;
            SwordStream swordstream;
            SwordSource swordsource;
            SwordPixelFormat swordpixelformat;
            SwordAttribute swordattribute;
            SwordValue swordvalue;
            string szFunction = "Deserialize";

            // Parse the JSON that we get back...
            JsonLookup jsonlookup = new JsonLookup();
            blSuccess = jsonlookup.Load(a_szTask, out lResponseCharacterOffset);
            if (!blSuccess)
            {
                TWAINWorkingGroup.Log.Error(szFunction + ": Load failed...");
                a_swordtask.SetTaskError("parseError", "fail", "", lResponseCharacterOffset);
                return (false);
            }

            // Instantiate the sword object...
            a_swordtask.m_guidScanner = a_guidScanner;
            a_swordtask.m_task = new Task();

            // Check the type of actions (make sure we find actions)...
            epropertytype = jsonlookup.GetType("actions");
            if ((epropertytype != JsonLookup.EPROPERTYTYPE.ARRAY) && (epropertytype != JsonLookup.EPROPERTYTYPE.UNDEFINED))
            {
                TWAINWorkingGroup.Log.Error("topology violation: actions isn't an array");
                a_swordtask.SetTaskError("invalidCapturingOptions", "actions", "invalidTask", 0);
                return (false);
            }

            // Walk the data.  As we walk it, we're going to build our data
            // structure, and do the following:
            //
            // - data items defined in outer levels cascade into inner
            //   levels, like exception
            //
            // - unrecognized content is discarded (types will fall into this
            //   category)
            //
            // - discard unrecognized vendor content...
            //
            // When done we'll have a structure that will already show some
            // of the culling process.  That process will continue as we go
            // on to try to set up the scanner.
            for (iAction = 0; true; iAction++)
            {
                // Break when we run out of actions...
                szSwordAction = "actions[" + iAction + "]";
                if (jsonlookup.Get(szSwordAction) == null)
                {
                    break;
                }

                // Add to the action array...
                if (a_swordtask.m_task.m_aswordaction == null)
                {
                    a_swordtask.m_task.m_aswordaction = new SwordAction[1];
                }
                else
                {
                    SwordAction[] aswordaction = new SwordAction[a_swordtask.m_task.m_aswordaction.Length + 1];
                    a_swordtask.m_task.m_aswordaction.CopyTo(aswordaction, 0);
                    a_swordtask.m_task.m_aswordaction = aswordaction;
                }
                a_swordtask.m_task.m_aswordaction[a_swordtask.m_task.m_aswordaction.Length - 1] = new SwordAction();
                swordaction = a_swordtask.m_task.m_aswordaction[a_swordtask.m_task.m_aswordaction.Length - 1];

                // Set the index...
                swordaction.m_szJsonKey = szSwordAction;

                // Set the action id...
                swordaction.m_szAction = jsonlookup.Get(szSwordAction + ".action");
                if (string.IsNullOrEmpty(swordaction.m_szAction))
                {
                    swordaction.m_szAction = "configuration";
                }

                // Set the action exception...
                swordaction.m_szException = jsonlookup.Get(szSwordAction + ".exception");
                if (string.IsNullOrEmpty(swordaction.m_szException))
                {
                    swordaction.m_szException = "ignore";
                }

                // Set the action vendor...
                swordaction.m_szVendor = jsonlookup.Get(szSwordAction + ".vendor");
                if (string.IsNullOrEmpty(swordaction.m_szVendor))
                {
                    swordaction.m_szVendor = "";
                }

                // Check the topology...
                if (    !a_swordtask.CheckTopology("actions", "", jsonlookup, ref a_swordtask)
                    ||  !a_swordtask.CheckTopology("action", szSwordAction, jsonlookup, ref a_swordtask))
                {
                    return (false);
                }

                // Set the status...
                swordaction.m_swordstatus = SwordStatus.Ready;

                // Check the type of streams (make sure we find streams)...
                epropertytype = jsonlookup.GetType(szSwordAction + ".streams");
                if ((epropertytype != JsonLookup.EPROPERTYTYPE.ARRAY) && (epropertytype != JsonLookup.EPROPERTYTYPE.UNDEFINED))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: streams isn't an array");
                    a_swordtask.SetTaskError("invalidCapturingOptions", szSwordAction + ".streams", "invalidTask", 0);
                    return (false);
                }

                ////////////////////////////////////////////////////////////////////////
                // Work on streams...
                for (iStream = 0; true; iStream++)
                {
                    // Break when we run out of streams...
                    szSwordStream = szSwordAction + ".streams[" + iStream + "]";
                    if (jsonlookup.Get(szSwordStream) == null)
                    {
                        break;
                    }

                    // Add to the stream array...
                    if (swordaction.m_aswordstream == null)
                    {
                        swordaction.m_aswordstream = new SwordStream[1];
                    }
                    else
                    {
                        SwordStream[] aswordstream = new SwordStream[swordaction.m_aswordstream.Length + 1];
                        swordaction.m_aswordstream.CopyTo(aswordstream, 0);
                        swordaction.m_aswordstream = aswordstream;
                    }
                    swordaction.m_aswordstream[swordaction.m_aswordstream.Length - 1] = new SwordStream();
                    swordstream = swordaction.m_aswordstream[swordaction.m_aswordstream.Length - 1];

                    // Set the index...
                    swordstream.m_szJsonKey = szSwordStream;

                    // Set the stream exception.
                    swordstream.m_szException = jsonlookup.Get(szSwordStream + ".exception");
                    if (string.IsNullOrEmpty(swordstream.m_szException))
                    {
                        // The task doesn't have an explicit exception.  If the action's
                        // exception is something other than ignore, we'll use that.
                        if (swordaction.m_szException != "ignore")
                        {
                            swordstream.m_szException = swordaction.m_szException;
                        }
                        // Otherwise, use nextStreamOrIgnore, this is a temporary placeholder
                        // that we'll repair later on.  We do it this way so that we can get
                        // the inheritance right for all the objects we contain at this level.
                        else
                        {
                            swordstream.m_szException = "@nextStreamOrIgnore";
                        }
                    }

                    // Set the stream vendor...
                    swordstream.m_szVendor = jsonlookup.Get(szSwordStream + ".vendor");
                    if (string.IsNullOrEmpty(swordstream.m_szVendor))
                    {
                        swordstream.m_szVendor = swordaction.m_szVendor;
                    }

                    // Check the topology...
                    if (    !a_swordtask.CheckTopology("streams", szSwordAction, jsonlookup, ref a_swordtask)
                        ||  !a_swordtask.CheckTopology("stream", szSwordStream, jsonlookup, ref a_swordtask))
                    {
                        return (false);
                    }

                    // Set the status...
                    swordstream.m_swordstatus = SwordStatus.Ready;

                    // Check the type of sources (make sure we find sources)...
                    epropertytype = jsonlookup.GetType(szSwordStream + ".sources");
                    if ((epropertytype != JsonLookup.EPROPERTYTYPE.ARRAY) && (epropertytype != JsonLookup.EPROPERTYTYPE.UNDEFINED))
                    {
                        TWAINWorkingGroup.Log.Error("topology violation: sources isn't an array");
                        a_swordtask.SetTaskError("invalidCapturingOptions", szSwordStream + ".sources", "invalidTask", 0);
                        return (false);
                    }

                    ////////////////////////////////////////////////////////////////////
                    // Work on sources...
                    for (iSource = 0; true; iSource++)
                    {
                        // Break when we run out of sources...
                        szSwordSource = szSwordStream + ".sources[" + iSource + "]";
                        if (jsonlookup.Get(szSwordSource) == null)
                        {
                            break;
                        }

                        // Add to the stream array...
                        if (swordstream.m_aswordsource == null)
                        {
                            swordstream.m_aswordsource = new SwordSource[1];
                        }
                        else
                        {
                            SwordSource[] aswordsource = new SwordSource[swordstream.m_aswordsource.Length + 1];
                            swordstream.m_aswordsource.CopyTo(aswordsource, 0);
                            swordstream.m_aswordsource = aswordsource;
                        }
                        swordstream.m_aswordsource[swordstream.m_aswordsource.Length - 1] = new SwordSource();
                        swordsource = swordstream.m_aswordsource[swordstream.m_aswordsource.Length - 1];

                        // Set the index...
                        swordsource.m_szJsonKey = szSwordSource;

                        // Set the source...
                        swordsource.m_szSource = jsonlookup.Get(szSwordSource + ".source");
                        if (string.IsNullOrEmpty(swordsource.m_szSource))
                        {
                            swordsource.m_szSource = "any";
                        }

                        // Set the source exception...
                        swordsource.m_szException = jsonlookup.Get(szSwordSource + ".exception");
                        if (string.IsNullOrEmpty(swordsource.m_szException))
                        {
                            swordsource.m_szException = swordstream.m_szException;
                        }

                        // Set the source vendor...
                        swordsource.m_szVendor = jsonlookup.Get(szSwordSource + ".vendor");
                        if (string.IsNullOrEmpty(swordsource.m_szVendor))
                        {
                            swordsource.m_szVendor = swordstream.m_szVendor;
                        }

                        // Check the topology...
                        if (    !a_swordtask.CheckTopology("sources", szSwordStream, jsonlookup, ref a_swordtask)
                            ||  !a_swordtask.CheckTopology("source", szSwordSource, jsonlookup, ref a_swordtask))
                        {
                            return (false);
                        }

                        // Set the status...
                        swordsource.m_swordstatus = SwordStatus.Ready;

                        // Check the type of pixelFormats (make sure we find pixelFormats)...
                        epropertytype = jsonlookup.GetType(szSwordSource + ".pixelFormats");
                        if ((epropertytype != JsonLookup.EPROPERTYTYPE.ARRAY) && (epropertytype != JsonLookup.EPROPERTYTYPE.UNDEFINED))
                        {
                            TWAINWorkingGroup.Log.Error("topology violation: pixelFormats isn't an array");
                            a_swordtask.SetTaskError("invalidCapturingOptions", szSwordSource + ".pixelFormats", "invalidTask", 0);
                            return (false);
                        }

                        ////////////////////////////////////////////////////////////////
                        // Work on pixel formats...
                        for (iPixelFormat = 0; true; iPixelFormat++)
                        {
                            // Break when we run out of pixelformats...
                            szSwordPixelFormat = szSwordSource + ".pixelFormats[" + iPixelFormat + "]";
                            if (jsonlookup.Get(szSwordPixelFormat) == null)
                            {
                                break;
                            }

                            // Add to the pixelformat array...
                            if (swordsource.m_aswordpixelformat == null)
                            {
                                swordsource.m_aswordpixelformat = new SwordPixelFormat[1];
                            }
                            else
                            {
                                SwordPixelFormat[] aswordformat = new SwordPixelFormat[swordsource.m_aswordpixelformat.Length + 1];
                                swordsource.m_aswordpixelformat.CopyTo(aswordformat, 0);
                                swordsource.m_aswordpixelformat = aswordformat;
                            }
                            swordsource.m_aswordpixelformat[swordsource.m_aswordpixelformat.Length - 1] = new SwordPixelFormat();
                            swordpixelformat = swordsource.m_aswordpixelformat[swordsource.m_aswordpixelformat.Length - 1];

                            // Set the index...
                            swordpixelformat.m_szJsonKey = szSwordPixelFormat;

                            // Set the pixelformat...
                            swordpixelformat.m_szPixelFormat = jsonlookup.Get(szSwordPixelFormat + ".pixelFormat");
                            if (string.IsNullOrEmpty(swordpixelformat.m_szPixelFormat))
                            {
                                swordpixelformat.m_szPixelFormat = "rgb24";
                            }

                            // Set the pixelformat exception...
                            swordpixelformat.m_szException = jsonlookup.Get(szSwordPixelFormat + ".exception");
                            if (string.IsNullOrEmpty(swordpixelformat.m_szException))
                            {
                                swordpixelformat.m_szException = swordsource.m_szException;
                            }

                            // Set the pixelformat vendor...
                            swordpixelformat.m_szVendor = jsonlookup.Get(szSwordPixelFormat + ".vendor");
                            if (string.IsNullOrEmpty(swordpixelformat.m_szVendor))
                            {
                                swordpixelformat.m_szVendor = swordsource.m_szVendor;
                            }

                            // Check the topology...
                            if (    !a_swordtask.CheckTopology("pixelFormats", szSwordSource, jsonlookup, ref a_swordtask)
                                ||  !a_swordtask.CheckTopology("pixelFormat", szSwordPixelFormat, jsonlookup, ref a_swordtask))
                            {
                                return (false);
                            }

                            // Set the status...
                            swordpixelformat.m_swordstatus = SwordStatus.Ready;

                            // Check the type of attributes (make sure we find attributes)...
                            epropertytype = jsonlookup.GetType(szSwordPixelFormat + ".attributes");
                            if ((epropertytype != JsonLookup.EPROPERTYTYPE.ARRAY) && (epropertytype != JsonLookup.EPROPERTYTYPE.UNDEFINED))
                            {
                                TWAINWorkingGroup.Log.Error("topology violation: attributes isn't an array");
                                a_swordtask.SetTaskError("invalidCapturingOptions", szSwordPixelFormat + ".attributes", "invalidTask", 0);
                                return (false);
                            }

                            ////////////////////////////////////////////////////////////
                            // Work on attributes...
                            for (iAttribute = 0; true; iAttribute++)
                            {
                                // Break when we run out of attributes...
                                szSwordAttribute = szSwordPixelFormat + ".attributes[" + iAttribute + "]";
                                if (jsonlookup.Get(szSwordAttribute) == null)
                                {
                                    break;
                                }

                                // Add to the attribute array...
                                if (swordpixelformat.m_aswordattribute == null)
                                {
                                    swordpixelformat.m_aswordattribute = new SwordAttribute[1];
                                }
                                else
                                {
                                    SwordAttribute[] aswordattribute = new SwordAttribute[swordpixelformat.m_aswordattribute.Length + 1];
                                    swordpixelformat.m_aswordattribute.CopyTo(aswordattribute, 0);
                                    swordpixelformat.m_aswordattribute = aswordattribute;
                                }
                                swordpixelformat.m_aswordattribute[swordpixelformat.m_aswordattribute.Length - 1] = new SwordAttribute();
                                swordattribute = swordpixelformat.m_aswordattribute[swordpixelformat.m_aswordattribute.Length - 1];

                                // Set the index...
                                swordattribute.m_szJsonKey = szSwordAttribute;

                                // Set the attribute id...
                                swordattribute.m_szAttribute = jsonlookup.Get(szSwordAttribute + ".attribute");
                                if (string.IsNullOrEmpty(swordattribute.m_szAttribute))
                                {
                                    swordattribute.m_szAttribute = "unspecifiedAttribute";
                                }

                                // Set the attribute exception...
                                swordattribute.m_szException = jsonlookup.Get(szSwordAttribute + ".exception");
                                if (string.IsNullOrEmpty(swordattribute.m_szException))
                                {
                                    swordattribute.m_szException = swordpixelformat.m_szException;
                                }

                                // Set the attribute vendor...
                                swordattribute.m_szVendor = jsonlookup.Get(szSwordAttribute + ".vendor");
                                if (string.IsNullOrEmpty(swordattribute.m_szVendor))
                                {
                                    swordattribute.m_szVendor = swordpixelformat.m_szVendor;
                                }

                                // Check the topology...
                                if (    !a_swordtask.CheckTopology("attributes", szSwordPixelFormat, jsonlookup, ref a_swordtask)
                                    ||  !a_swordtask.CheckTopology("attribute", szSwordAttribute, jsonlookup, ref a_swordtask))
                                {
                                    return (false);
                                }

                                // Set the status...
                                swordattribute.m_swordstatus = SwordStatus.Ready;

                                // Check the type of values (make sure we find values)...
                                epropertytype = jsonlookup.GetType(szSwordAttribute + ".values");
                                if ((epropertytype != JsonLookup.EPROPERTYTYPE.ARRAY) && (epropertytype != JsonLookup.EPROPERTYTYPE.UNDEFINED))
                                {
                                    TWAINWorkingGroup.Log.Error("topology violation: values isn't an array");
                                    a_swordtask.SetTaskError("invalidCapturingOptions", szSwordAttribute + ".values", "invalidTask", 0);
                                    return (false);
                                }

                                ////////////////////////////////////////////////////////
                                // Work on values...
                                for (iValue = 0; true; iValue++)
                                {
                                    // Break when we run out of values...
                                    szSwordValue = szSwordAttribute + ".values[" + iValue + "]";
                                    if (jsonlookup.Get(szSwordValue) == null)
                                    {
                                        break;
                                    }

                                    // Add to the value array...
                                    if (swordattribute.m_aswordvalue == null)
                                    {
                                        swordattribute.m_aswordvalue = new SwordValue[1];
                                    }
                                    else
                                    {
                                        SwordValue[] aswordvalue = new SwordValue[swordattribute.m_aswordvalue.Length + 1];
                                        swordattribute.m_aswordvalue.CopyTo(aswordvalue, 0);
                                        swordattribute.m_aswordvalue = aswordvalue;
                                    }
                                    swordattribute.m_aswordvalue[swordattribute.m_aswordvalue.Length - 1] = new SwordValue();
                                    swordvalue = swordattribute.m_aswordvalue[swordattribute.m_aswordvalue.Length - 1];

                                    // Set the index...
                                    swordvalue.m_szJsonKey = szSwordValue;

                                    // Set the value exception...
                                    swordvalue.m_szException = jsonlookup.Get(szSwordValue + ".exception");
                                    if (string.IsNullOrEmpty(swordvalue.m_szException))
                                    {
                                        swordvalue.m_szException = swordattribute.m_szException;
                                    }

                                    // Set the value vendor...
                                    swordvalue.m_szVendor = jsonlookup.Get(szSwordValue + ".vendor");
                                    if (string.IsNullOrEmpty(swordvalue.m_szVendor))
                                    {
                                        swordvalue.m_szVendor = swordattribute.m_szVendor;
                                    }

                                    // Set the value ...
                                    swordvalue.m_szValue = jsonlookup.Get(szSwordValue + ".value");
                                    if (string.IsNullOrEmpty(swordvalue.m_szValue))
                                    {
                                        swordvalue.m_szValue = "";
                                    }

                                    // Check the topology...
                                    if (    !a_swordtask.CheckTopology("values", szSwordAttribute, jsonlookup, ref a_swordtask)
                                        ||  !a_swordtask.CheckTopology("value", szSwordValue, jsonlookup, ref a_swordtask))
                                    {
                                        return (false);
                                    }

                                    // Set the status...
                                    swordvalue.m_swordstatus = SwordStatus.Ready;
                                }
                            }
                        }
                    }
                }
            }

            // Fix the exceptions.  This just makes the code easier to handle downstream, since
            // we only have to worry about nextStream and ignore, without trying to work out the
            // context, based on where we are in the array.
            if ((a_swordtask != null) && (a_swordtask.m_task != null) && (a_swordtask.m_task.m_aswordaction != null))
            {
                SwordAction[] aswordaction = a_swordtask.m_task.m_aswordaction;
                for (iAction = 0; iAction < aswordaction.Length; iAction++)
                {
                    // Fix the exception...
                    if (aswordaction[iAction].m_szException == "@nextStreamOrIgnore")
                    {
                        aswordaction[iAction].m_szException = ((iAction + 1) < aswordaction.Length) ? "nextStream" : "ignore";
                    }

                    // Check the streams...
                    if (aswordaction[iAction].m_aswordstream != null)
                    {
                        // Check the streams...
                        SwordStream[] aswordstream = aswordaction[iAction].m_aswordstream;
                        for (iStream = 0; iStream < aswordstream.Length; iStream++)
                        {
                            // If we don't have this here, then we can't have it further down...
                            if (aswordstream[iStream].m_szException != "@nextStreamOrIgnore")
                            {
                                continue;
                            }

                            // Fix the exception based on where we are in the array...
                            aswordstream[iStream].m_szException = ((iStream + 1) < aswordstream.Length) ? "nextStream" : "ignore";

                            // Check the sources...
                            if (aswordstream[iStream].m_aswordsource != null)
                            {
                                SwordSource[] aswordsource = aswordstream[iStream].m_aswordsource;
                                for (iSource = 0; iSource < aswordsource.Length; iSource++)
                                {
                                    // Fix the exception...
                                    if (aswordsource[iSource].m_szException == "@nextStreamOrIgnore")
                                    {
                                        aswordsource[iSource].m_szException = aswordstream[iStream].m_szException;
                                    }

                                    // Check the pixel formats...
                                    if (aswordsource[iSource].m_aswordpixelformat != null)
                                    {
                                        SwordPixelFormat[] aswordpixelformat = aswordsource[iSource].m_aswordpixelformat;
                                        for (iPixelFormat = 0; iPixelFormat < aswordpixelformat.Length; iPixelFormat++)
                                        {
                                            // Fix the exception...
                                            if (aswordpixelformat[iPixelFormat].m_szException == "@nextStreamOrIgnore")
                                            {
                                                aswordpixelformat[iPixelFormat].m_szException = aswordstream[iStream].m_szException;
                                            }

                                            // Check the attributes...
                                            if (aswordpixelformat[iPixelFormat].m_aswordattribute != null)
                                            {
                                                SwordAttribute[] aswordattribute = aswordpixelformat[iPixelFormat].m_aswordattribute;
                                                for (iAttribute = 0; iAttribute < aswordattribute.Length; iAttribute++)
                                                {
                                                    // Fix the exception...
                                                    if (aswordattribute[iAttribute].m_szException == "@nextStreamOrIgnore")
                                                    {
                                                        aswordattribute[iAttribute].m_szException = aswordstream[iStream].m_szException;

                                                        // Check the values...
                                                        if (aswordattribute[iAttribute].m_aswordvalue != null)
                                                        {
                                                            SwordValue[] aswordvalue = aswordattribute[iAttribute].m_aswordvalue;
                                                            for (iValue = 0; iValue < aswordvalue.Length; iValue++)
                                                            {
                                                                // Fix the exception...
                                                                if (aswordvalue[iValue].m_szException == "@nextStreamOrIgnore")
                                                                {
                                                                    aswordvalue[iValue].m_szException = aswordattribute[iAttribute].m_szException;
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // We're good...
            return (true);
        }

        /// <summary>
        /// Return the task object...
        /// </summary>
        /// <returns>the object we built from the JSON string</returns>
        public Task GetTask()
        {
            return (m_task);
        }

        /// <summary>
        /// Return the exception value...
        /// </summary>
        /// <returns>the exception that stopped us or success</returns>
        public string GetException()
        {
            if (m_szException == null)
            {
                return ("success");
            }
            return (m_szException);
        }

        /// <summary>
        /// Returns the owner of the GUID...
        /// </summary>
        /// <param name="a_szGuid">GUID to test</param>
        /// <returns>who own it</returns>
        public GuidOwner GetGuidOwner(string a_szGuid)
        {
            Guid guid;

            // If we have no data, we assume TWAIN Direct...
            if (string.IsNullOrEmpty(a_szGuid))
            {
                return (GuidOwner.TwainDirect);
            }

            // Convert it, if we fail, assume unknown...
            try
            {
                guid = new Guid(a_szGuid);
            }
            catch
            {
                TWAINWorkingGroup.Log.Error("Invalid GUID format...");
                return (GuidOwner.Unknown);
            }

            // The standard guid...
            if (guid == m_guidTwainDirect)
            {
                return (GuidOwner.TwainDirect);
            }

            // The scanner's guid...
            if (guid == m_guidScanner)
            {
                return (GuidOwner.Scanner);
            }

            // The escape clause...
            return (GuidOwner.Unknown);
        }

        /// <summary>
        /// Returns the index into the original JSON string where processing stopped...
        /// </summary>
        /// <returns>an index value, -1 on success</returns>
        public long GetJsonErrorIndex()
        {
            return (m_lResponseCharacterOffset);
        }

        /// <summary>
        /// Return the JSON key where the exception occurred...
        /// </summary>
        /// <returns>the key in dotted notation</returns>
        public string GetJsonExceptionKey()
        {
            if (m_szJsonExceptionKey == null)
            {
                return ("(null)");
            }
            return (m_szJsonExceptionKey);
        }

        /// <summary>
        /// Return SWORD value that we ended up using...
        /// </summary>
        /// <returns>the SWORD value or null</returns>
        public string GetSwordValue()
        {
            return (m_szSwordValue);
        }

        /// <summary>
        /// Return TWAIN value that we ended up using...
        /// </summary>
        /// <returns>the TWAIN value or null</returns>
        public string GetTwainValue()
        {
            if (m_szTwainValue == null)
            {
                return ("(null)");
            }
            return (m_szTwainValue);
        }

        /// <summary>
        /// Return the task reply...
        /// </summary>
        /// <returns>the complete reply task or null</returns>
        public string GetTaskReply()
        {
            return (m_szTaskReply);
        }

        /// <summary>
        /// Set a task error...
        /// </summary>
        /// <param name="a_szException">the exception</param>
        /// <param name="m_szJsonExceptionKey">the location</param>
        /// <param name="a_szTwainValue">the value</param>
        /// <param name="a_lResponseCharacterOffset">index into JSON string where processing stopped</param>
        public void SetTaskError(string a_szException, string a_szJsonExceptionKey, string a_szTwainValue, long a_lResponseCharacterOffset)
        {
            // Ruh-roh...
            if (!m_blSuccess)
            {
                TWAINWorkingGroup.Log.Error("We've already processed an error...so ignoring this one...");
                TWAINWorkingGroup.Log.Error(a_szException + ": " + a_szJsonExceptionKey);
                return;
            }

            // Record the error...
            m_blSuccess = false;
            m_szException = a_szException;
            m_szJsonExceptionKey = a_szJsonExceptionKey;
            m_szTwainValue = a_szTwainValue;
            m_lResponseCharacterOffset = a_lResponseCharacterOffset;
        }

        /// <summary>
        /// Set the task reply...
        /// </summary>
        /// <param name="a_szTaskReply">the task data we want to store</param>
        public void SetTaskReply(string a_szTaskReply)
        {
            m_szTaskReply = a_szTaskReply;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Public Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Definitions...

        /// <summary>
        /// Tells us the owner of the guid...
        /// </summary>
        public enum GuidOwner
        {
            TwainDirect,    // owned by TWAIN Direct
            Scanner,        // owned by the current scanner
            Unknown         // everybody else
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods...

        /// <summary>
        /// We want to make sure that tasks follow a strict topology order, this
        /// means that terms in that topology cannot appear out od sequence...
        /// </summary>
        /// <param name="a_szKey">the current key in the topology (ex: action, stream)</param>
        /// <param name="a_szKey">the current path to the key</param>
        /// <param name="a_jsonlookup">task that we're testing</param>
        /// <returns></returns>
        private bool CheckTopology(string a_szKey, string a_szPath, JsonLookup a_jsonlookup, ref SwordTask a_swordtask)
        {
            string szFullKey;

            // If this is custom to a vendor, then skip this test, we can't
            // know what they're doing, so we shouldn't try to check it...
            szFullKey = a_szPath + ((a_szPath != "") ? ".vendor" : "vendor");
            if (GetGuidOwner(a_jsonlookup.Get(szFullKey)) == SwordTask.GuidOwner.Unknown)
            {
                return (true);
            }

            // If we find actions, but it's not an actions array, we have a problem...
            if (a_szKey != "actions")
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".actions" : "actions");
                if (!string.IsNullOrEmpty(a_jsonlookup.Get(szFullKey)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: actions");
                    a_swordtask.SetTaskError("invalidCapturingOptions", szFullKey, "invalidTask", -1);
                    return (false);
                }
            }

            // If we find action, but it's not an action key or a streams array, we have a problem...
            if ((a_szKey != "action") && (a_szKey != "streams"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".action" : "action");
                if (!string.IsNullOrEmpty(a_jsonlookup.Get(szFullKey)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: action");
                    a_swordtask.SetTaskError("invalidCapturingOptions", szFullKey, "invalidTask", -1);
                    return (false);
                }
            }

            // If we find streams, but it's not an action key or a streams array, we have a problem...
            if ((a_szKey != "streams") && (a_szKey != "action"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".streams" : "streams");
                if (!string.IsNullOrEmpty(a_jsonlookup.Get(szFullKey)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: streams");
                    a_swordtask.SetTaskError("invalidCapturingOptions", szFullKey, "invalidTask", -1);
                    return (false);
                }
            }

            // If we find stream, but it's not a stream key or a sources array, we have a problem...
            if ((a_szKey != "stream") && (a_szKey != "sources"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".stream" : "stream");
                if (!string.IsNullOrEmpty(a_jsonlookup.Get(szFullKey)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: stream");
                    a_swordtask.SetTaskError("invalidCapturingOptions", szFullKey, "invalidTask", -1);
                    return (false);
                }
            }

            // If we find sources, but it's not a stream key or a sources array, we have a problem...
            if ((a_szKey != "sources") && (a_szKey != "stream"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".sources" : "sources");
                if (!string.IsNullOrEmpty(a_jsonlookup.Get(szFullKey)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: sources");
                    a_swordtask.SetTaskError("invalidCapturingOptions", szFullKey, "invalidTask", -1);
                    return (false);
                }
            }

            // If we find source, but it's not a source key or a pixelformats array, we have a problem...
            if ((a_szKey != "source") && (a_szKey != "pixelFormats"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".source" : "source");
                if (!string.IsNullOrEmpty(a_jsonlookup.Get(szFullKey)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: source");
                    a_swordtask.SetTaskError("invalidCapturingOptions", szFullKey, "invalidTask", -1);
                    return (false);
                }
            }

            // // If we find pixelformats, but it's not a source key or a pixelformats array, we have a problem...
            if ((a_szKey != "pixelFormats") && (a_szKey != "source"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".pixelFormats" : "pixelFormats");
                if (!string.IsNullOrEmpty(a_jsonlookup.Get(szFullKey)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: pixelFormats");
                    a_swordtask.SetTaskError("invalidCapturingOptions", szFullKey, "invalidTask", -1);
                    return (false);
                }
            }

            // If we find pixelformat, but it's not a pixelformat key or an attributes array, we have a problem...
            if ((a_szKey != "pixelFormat") && (a_szKey != "attributes"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".pixelFormat" : "pixelFormat");
                if (!string.IsNullOrEmpty(a_jsonlookup.Get(szFullKey)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: pixelFormat");
                    a_swordtask.SetTaskError("invalidCapturingOptions", szFullKey, "invalidTask", -1);
                    return (false);
                }
            }

            // If we find attributes, but it's not a pixelformat key or an attributes array, we have a problem...
            if ((a_szKey != "attributes") && (a_szKey != "pixelFormat"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".attributes" : "attributes");
                if (!string.IsNullOrEmpty(a_jsonlookup.Get(szFullKey)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: attributes");
                    a_swordtask.SetTaskError("invalidCapturingOptions", szFullKey, "invalidTask", -1);
                    return (false);
                }
            }

            // If we find attribute, but it's not an attribute key or a value array, we have a problem...
            if ((a_szKey != "attribute") && (a_szKey != "values"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".attribute" : "attribute");
                if (!string.IsNullOrEmpty(a_jsonlookup.Get(szFullKey)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: attribute");
                    a_swordtask.SetTaskError("invalidCapturingOptions", szFullKey, "invalidTask", -1);
                    return (false);
                }
            }

            // If we find values, but it's not an attribute key or a value array, we have a problem...
            if ((a_szKey != "values") && (a_szKey != "attribute"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".values" : "values");
                if (!string.IsNullOrEmpty(a_jsonlookup.Get(szFullKey)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: values");
                    a_swordtask.SetTaskError("invalidCapturingOptions", szFullKey, "invalidTask", -1);
                    return (false);
                }
            }

            // If we find value, but it's not a value key, we have a problem...
            if ((a_szKey != "value"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".value" : "value");
                if (!string.IsNullOrEmpty(a_jsonlookup.Get(szFullKey)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: value");
                    a_swordtask.SetTaskError("invalidCapturingOptions", szFullKey, "invalidTask", -1);
                    return (false);
                }
            }

            // We're good...
            return (true);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        /// <summary>
        /// The fully processed task.  This is a dynamic class heirarchy representing
        /// the TWAIN Direct task that was passed to us as a JSON string...
        /// </summary>
        private Task m_task;

        /// <summary>
        /// The TWAIN Direct GUID...
        /// </summary>
        private Guid m_guidTwainDirect;

        /// <summary>
        /// The scanner's GUID (optional)...
        /// </summary>
        private Guid m_guidScanner;

        /// <summary>
        /// True as long as the task is successful, we anticipate success, so that's
        /// our starting point.  If this value goes to false, then further processing
        /// must stop, and the error info at the point of failure ia returned...
        /// </summary>
        private bool m_blSuccess;

        /// <summary>
        /// The exception we're reporting on failure...
        /// </summary>
        private string m_szException;

        /// <summary>
        /// The location in the task, returned in the dotted key notation
        /// (ex: action[0].stream[0].source[0]
        /// </summary>
        private string m_szJsonExceptionKey;

        /// <summary>
        /// SWORD value that we used...
        /// </summary>
        private string m_szSwordValue;

        /// <summary>
        /// TWAIN value that we used...
        /// </summary>
        private string m_szTwainValue;

        /// <summary>
        /// If the JSON can't be parsed, this value will (hopefully) point to where
        /// the error occurred...
        /// </summary>
        private long m_lResponseCharacterOffset;

        /// <summary>
        /// If the task is accepted, then this string contains a snapshot of the work
        /// the task is going to do.
        /// </summary>
        private string m_szTaskReply;

        #endregion
    }


    /// <summary>
    /// A particular SWORD task, which consists of a unique
    /// id and an array of actions.
    /// </summary>
    public class Task
    {
        public Task()
        {
            m_szId = null;
        }

        // The unique UUID for this task
        public string m_szId;

        // A collection of actions;
        public SwordAction[] m_aswordaction;
    }

    /// <summary>
    /// The list of actions, as long as there are no unrecoverable
    /// errors, each one of these will be run.
    /// </summary>
    public class SwordAction
    {
        public SwordAction()
        {
            m_swordstatus = SwordStatus.Undefined;
            m_szJsonKey = null;
            m_szException = null;
            m_szAction = null;
            m_szVendor = null;
            m_aswordstream = null;
        }

        // The status of the item...
        public SwordStatus m_swordstatus;

        // The index of this item in the JSON string...
        public string m_szJsonKey;

        // The exception for this action
        public string m_szException;

        // The command identifier...
        public string m_szAction;

        // Vendor UUID...
        public string m_szVendor;

        // The image streams...
        public SwordStream[] m_aswordstream;
    }

    /// <summary>
    /// Each stream contains a list of sources.  All of the sources
    /// are used to capture image data.  This can result in some odd
    /// but perfectly acceptable combinations: such as a feeder and
    /// a flatbed, in which case the session would capture all of the
    /// data from the feeder, then an image from the flatbed.
    /// 
    /// The more typical example would be a feeder or a flatbed, or
    /// separate settings for the front and rear of a feeder.
    /// </summary>
    public class SwordStream
    {
        public SwordStream()
        {
            m_swordstatus = SwordStatus.Undefined;
            m_szJsonKey = null;
            m_szException = null;
            m_szVendor = null;
            m_aswordsource = null;
        }

        // The status of the item...
        public SwordStatus m_swordstatus;

        // The index of this item in the JSON string...
        public string m_szJsonKey;

        // The default exception for this stream...
        public string m_szException;

        // Vendor UUID...
        public string m_szVendor;

        // The image sources...
        public SwordSource[] m_aswordsource;
    }

    /// <summary>
    /// Each source corresponds to a physical element
    /// that captures image data, like a front or rear
    /// camera on a feeder or a flatbed.  If multiple
    /// sources are included then multistream is being
    /// requested.
    /// 
    /// The use of the "any" source is a shorthand for
    /// a stream that asks for images from every source
    /// the scanner has to offer.  It should only be
    /// used in the simplest cases or as a exception
    /// if other sources have failed.
    /// </summary>
    public class SwordSource
    {
        public SwordSource()
        {
            m_swordstatus = SwordStatus.Undefined;
            m_szJsonKey = null;
            m_szException = null;
            m_szSource = null;
            m_szVendor = null;
            m_aswordpixelformat = null;
        }

        // The status of the item...
        public SwordStatus m_swordstatus;

        // The index of this item in the JSON string...
        public string m_szJsonKey;

        // The default exception for all items in this source
        public string m_szException;

        // Source of images (ex: any, feederduplex, feederfront, flatbed, etc)
        public string m_szSource;

        // Vendor UUID...
        public string m_szVendor;

        /// <summary>
        /// The format list contains one or more formats.  This may
        /// correspond to physical capture elements, but more usually
        /// are capture settings on a source.  If multiple formats
        /// appear in the same source it's an "OR" operation.  The
        /// best fit is selected.  This is how the imageformat can be
        /// automatically detected.
        /// </summary>
        public SwordPixelFormat[] m_aswordpixelformat;
    }

    /// <summary>
    /// A format within a source...
    /// </summary>
    public class SwordPixelFormat
    {
        public SwordPixelFormat()
        {
            m_swordstatus = SwordStatus.Undefined;
            m_szJsonKey = null;
            m_szException = null;
            m_szPixelFormat = null;
            m_szVendor = null;
            m_aswordattribute = null;
        }

        // The status of the item...
        public SwordStatus m_swordstatus;

        // The index of this item in the JSON string...
        public string m_szJsonKey;

        // The default exception for all items in this source
        public string m_szException;

        // Format of images (ex: bw1, gray8, rgb24, etc)
        public string m_szPixelFormat;

        // Vendor UUID...
        public string m_szVendor;

        // The attributes...
        public SwordAttribute[] m_aswordattribute;
    }

    /// <summary>
    /// An attribute...
    /// </summary>
    public class SwordAttribute
    {
        public SwordAttribute()
        {
            m_swordstatus = SwordStatus.Undefined;
            m_szJsonKey = null;
            m_szException = null;
            m_szAttribute = null;
            m_szVendor = null;
            m_aswordvalue = null;
        }

        // The status of the item...
        public SwordStatus m_swordstatus;

        // The index of this item in the JSON string...
        public string m_szJsonKey;

        // The exception for this attribute
        public string m_szException;

        // The id of the attribute
        public string m_szAttribute;

        // Vendor UUID...
        public string m_szVendor;

        // The values...
        public SwordValue[] m_aswordvalue;
    }

    /// <summary>
    /// A value for an attribute...
    /// </summary>
    public class SwordValue
    {
        public SwordValue()
        {
            m_swordstatus = SwordStatus.Undefined;
            m_szJsonKey = null;
            m_szException = null;
            m_szValue = null;
            m_szVendor = null;
        }

        // The status of the item...
        public SwordStatus m_swordstatus;

        // The index of this item in the JSON string...
        public string m_szJsonKey;

        // The exception for this value
        public string m_szException;

        // A single value
        public string m_szValue;

        // Vendor UUID...
        public string m_szVendor;
    }

    /// <summary>
    /// These are all the possible outcomes of TWAIN's attempt to use
    /// the task.  By setting these values we'll be able to go back
    /// and create a task that reflects the settings that were actually
    /// used...
    /// </summary>
    public enum SwordStatus
    {
        Undefined,
        Success,
        SuccessIgnore,
        Fail,
        BadValue,
        Next,
        Ready,
        Unsupported
    }

    #endregion


    /// <summary>
    /// Track the data needed for the response to a task...
    /// </summary>
    #region SwordTaskResponse

    // Our class...
    public sealed class SwordTaskResponse
    {
        #region Public Methods

        /// <summary>
        /// Init stuff...
        /// </summary>
        public SwordTaskResponse()
        {
            // non-zero stuff...
            Clear();

            // Pack our JSON output...
            m_blPack = (Config.Get("packJson", "true") == "true");
        }

        /// <summary>
        /// Clear any current error information...
        /// </summary>
        public void Clear()
        {
            // Leave the task response buffer alone...
            m_blSuccess = true;
            m_lJsonErrorIndex = -1;
            m_szException = "";
            m_szJsonExceptionKey = "";
            m_szLexiconValue = "";
            m_szTaskResponse = "";
        }

        /// <summary>
        /// Get the task response...
        /// </summary>
        /// <returns>the task response</returns>
        public string GetTaskResponse()
        {
            return (m_szTaskResponse);
        }

        /// <summary>
        /// Set a task error...
        /// </summary>
        public void SetError
        (
            string a_szException,
            string a_szJsonExceptionKey,
            string a_szLexiconValue,
            long a_lJsonErrorIndex
        )
        {
            // Ruh-roh, we've already done this...
            if (!m_blSuccess)
            {
                // We can't log anything here, it would be too noisy...
                return;
            }

            // Record the error...
            Clear();
            m_blSuccess = false;
            m_szException = a_szException;
            m_szJsonExceptionKey = a_szJsonExceptionKey;
            m_szLexiconValue = a_szLexiconValue;
            m_lJsonErrorIndex = a_lJsonErrorIndex;

            // Build the task reply, since we're just parsing the JSON
            // the only kind of errors we can run into will be invalid
            // JSON complaints...
            JSON_ROOT_BGN();                                                     // start root
            JSON_ARR_BGN(1, "actions");                                          // start actions array
            JSON_OBJ_BGN(2, "");                                                 // start action object
            JSON_STR_SET(3, "action", ",", "");                                  // action property,
            JSON_OBJ_BGN(3, "results");                                          // start results object
            JSON_TOK_SET(4, "success", ",", "false");                            // success property,
            JSON_STR_SET(4, "code", ",", a_szLexiconValue);                      // code property,
            if (a_szLexiconValue == "invalidJson")
            {
                JSON_NUM_SET(4, "characterOffset", "", (int)m_lJsonErrorIndex);  // characterOffset property
            }
            if (!string.IsNullOrEmpty(a_szJsonExceptionKey))
            {
                JSON_STR_SET(4, "jsonKey", "", m_szJsonExceptionKey);            // jsonKey property
            }
            JSON_OBJ_END(3, "");                                                 // end response object
            JSON_OBJ_END(2, "");                                                 // end action object
            JSON_ARR_END(1, "");                                                 // end actions array
            JSON_ROOT_END();                                                     // end root
        }

        /// <summary>
        /// Set the pack flag...
        /// </summary>
        /// <param name="a_blPack"></param>
        public void SetPack
        (
            bool a_blPack
        )
        {
            m_blPack = a_blPack;
        }

        // A standalone newline marker...
        public void JSONEOLN()
        {
            m_szTaskResponse += (m_blPack ? "" : Environment.NewLine);
        }

        // Root begin / end...
        public void JSON_ROOT_BGN()
        {
            m_szTaskResponse = m_blPack ? "{" : ("{" + Environment.NewLine);
        }
        public void JSON_ROOT_END()
        {
            m_szTaskResponse += "}";
        }

        // Array begin / end...
        public void JSON_ARR_BGN(int tab, string name)
        {
            m_szTaskResponse += !string.IsNullOrEmpty(name) ? (m_blPack ? ("\"" + name + "\":[") : (Tabs(tab) + "\"" + name + "\": [" + Environment.NewLine)) : (m_blPack ? "[" : (Tabs(tab) + "[" + Environment.NewLine));
        }
        public void JSON_ARR_END(int tab, string comma)
        {
            m_szTaskResponse += m_blPack ? ("]" + comma) : (Tabs(tab) + "]" + comma + Environment.NewLine);
        }

        // Object begin / end...
        public void JSON_OBJ_BGN(int tab, string name)
        {
            m_szTaskResponse += !string.IsNullOrEmpty(name) ? (m_blPack ? ("\"" + name + "\":{") : (Tabs(tab) + "\"" + name + "\": {" + Environment.NewLine)) : (m_blPack ? "{" : (Tabs(tab) + "{" + Environment.NewLine));
        }
        public void JSON_OBJ_END(int tab, string comma)
        {
            m_szTaskResponse += m_blPack ? ("}" + comma) : (Tabs(tab) + "}" + comma + Environment.NewLine);
        }

        // Strings, tokens (null,true,false), and numbers...
        public void JSON_STR_SET(int tab, string name, string comma, string str)
        {
            m_szTaskResponse += m_blPack ? ("\"" + name + "\":\"" + str + "\"" + comma) : (Tabs(tab) + "\"" + name + "\": \"" + str + "\"" + comma + Environment.NewLine);
        }
        public void JSON_TOK_SET(int tab, string name, string comma, string str)
        {
            m_szTaskResponse += m_blPack ? ("\"" + name + "\":" + str + comma) : (Tabs(tab) + "\"" + name + "\": " + str + comma + Environment.NewLine);
        }
        public void JSON_NUM_SET(int tab, string name, string comma, int num)
        {
            m_szTaskResponse += m_blPack ? ("\"" + name + "\":" + num + comma) : (Tabs(tab) + "\"" + name + "\": " + num + comma + Environment.NewLine);
        }

        #endregion


        /// <summary>
        /// Private Methods
        /// </summary>
        #region Private Methods

        // Generate tabs...
        private string Tabs(int a_iTotal)
        {
            if (a_iTotal <= 0)
            {
                return ("");
            }
            return (new string('\t', a_iTotal));
        }

        #endregion


        /// <summary>
        /// Private Definitions
        /// </summary>
        #region Private Definitions

        // TWAIN Direct lookup stuff, map TWAIN Direct attributes to
        // their corresponding CDatabase Id's, if we have one...
        static readonly string[,] s_atwaindirectlookup = new string[,]
        {
            { "alarms",                             "CAP_ALARMS" },
            { "alarmVolume",                        "CAP_ALARMVOLUME" },
            { "automaticDeskew",                    "CAP_AUTOMATICDESKEW" },
            { "automaticSize",                      "CAP_AUTOSIZE" },
            { "barcodes",                           "ICAP_BARCODEDETECTIONENABLED" },
            { "bitDepthReduction",                  "ICAP_BITDEPTHREDUCTION" },
            { "brightness",                         "ICAP_BRIGHTNESS" },
            { "compression",                        "ICAP_COMPRESSION" },
            { "continuousScan",                     "CAP_AUTOSCAN" },
            { "contrast",                           "ICAP_CONTRAST" },
            { "cropping",                           "ICAP_AUTOMATICBORDERDETECTION" },
            { "discardBlankImages",                 "ICAP_AUTODISCARDBLANKPAGES" },
            { "doubleFeedDetection",                "CAP_DOUBLEFEEDDETECTION" },
            { "doubleFeedDetectionLength",          "CAP_DOUBLEFEEDDETECTIONLENGTH" },
            { "doubleFeedDetectionResponse",        "CAP_DOUBLEFEEDDETECTIONRESPONSE" },
            { "doubleFeedDetectionSensitivity",     "CAP_DOUBLEFEEDDETECTIONSENSITIVITY" },
            { "flipRotation",                       "ICAP_FLIPROTATION" },
            { "height",                             "ICAP_FRAME" },
            { "imageMerge",                         "ICAP_IMAGEMERGE" },
            { "imageMergeHeightThreshold",          "ICAP_IMAGEMERGEHEIGHTTHREADHOLD" },
            { "invert",                             "ICAP_PIXELFLAVOR" },
            { "jpegQuality",                        "ICAP_JPEGQUALITY" },
            { "micr",                               "CAP_MICRENABLED" },
            { "mirror",                             "ICAP_MIRROR" },
            { "noiseFilter",                        "ICAP_NOISEFILTER" },
            { "overScan",                           "ICAP_OVERSCAN" },
            { "numberOfSheets",                     "CAP_SHEETCOUNT" },
            { "offsetX",                            "ICAP_FRAME" },
            { "offsetY",                            "ICAP_FRAME" },
            { "patchCodes",                         "ICAP_PATCHCODEDETECTIONENABLED" },
            { "resolution",                         "CAP_XRESOLUTION" },
            { "rotation",                           "ICAP_ROTATION" },
            { "sheetHandling",                      "CAP_FEEDERMODE" },
            { "sheetSize",                          "ICAP_SUPPORTEDSIZES" },
            { "threshold",                          "ICAP_THRESHOLD" },
            { "uncalibratedImage",                  "xxx" },
            { "width",                              "ICAP_FRAME" },
	        // Must be last...
	        { "",                                   "" }
        };

        #endregion


        /// <summary>
        /// Private Attributes
        /// </summary>
        #region Private Attributes

        // True as long as the task is successful, we anticipate success, so that's
        // our starting point.  If this value goes to false, then further processing
        // must stop, and the error info at the point of failure is returned...
        private bool m_blSuccess;

        // The exception we're reporting on failure...
        private string m_szException;

        // The location in the task, returned in the dotted key notation
        // (ex: action[0].stream[0].source[0]
        private string m_szJsonExceptionKey;

        // Lexicon value that we used...
        private string m_szLexiconValue;

        // If the JSON can't be parsed, this value will (hopefully) point to where
        // the error occurred...
        private long m_lJsonErrorIndex;

        // Our task reply buffer, and its size...
        private string m_szTaskResponse;

        // Pack the data in our response...
        private bool m_blPack;

        #endregion
    }

    #endregion
}