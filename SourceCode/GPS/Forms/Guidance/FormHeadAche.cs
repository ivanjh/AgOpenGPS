﻿using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

namespace AgOpenGPS
{
    public partial class FormHeadAche : Form
    {
        //access to the main GPS form and all its variables
        private readonly FormGPS mf = null;

        private Point fixPt;

        private bool isA = true;
        private int start = 99999, end = 99999;
        private int bndSelect = 0;

        public vec3 pint = new vec3(0.0, 1.0, 0.0);

        private bool isLinesVisible = true;

        public FormHeadAche(Form callingForm)
        {
            //get copy of the calling main form
            mf = callingForm as FormGPS;

            InitializeComponent();
            mf.CalculateMinMax();
        }

        private void FormHeadLine_Load(object sender, EventArgs e)
        {
            mf.hdl.idx = -1;

            mf.FileLoadHeadLines();
            FixLabelsCurve();

            label3.Text = mf.unitsFtM;
            lblToolWidth.Text = ((mf.tool.width - mf.tool.overlap) * mf.m2FtOrM).ToString("N1") + " " + mf.unitsFtM;
        }

        private void FormHeadLine_FormClosing(object sender, FormClosingEventArgs e)
        {
            //hdl
            if (mf.hdl.idx == -1)
            {
                if (mf.isAutoSteerBtnOn) mf.btnAutoSteer.PerformClick();
                if (mf.yt.isYouTurnBtnOn) mf.btnAutoYouTurn.PerformClick();
            }

            mf.FileSaveHeadLines();

            if (mf.hdl.tracksArr.Count > 0)
            {
                mf.hdl.idx = 0;
            }
            else mf.hdl.idx = -1;
        }

        private void FixLabelsCurve()
        {
            lblNumCu.Text = mf.hdl.tracksArr.Count.ToString();
            lblCurveSelected.Text = (mf.hdl.idx+1).ToString();
        }

        private void btnCycleForward_Click(object sender, EventArgs e)
        {
            mf.bnd.bndList[0].hdLine?.Clear();

            if (mf.hdl.tracksArr.Count > 0)
            {
                mf.hdl.idx++;
                if (mf.hdl.idx > (mf.hdl.tracksArr.Count - 1)) mf.hdl.idx = 0;
            }
            else
            {
                mf.hdl.idx = -1;
            }

            FixLabelsCurve();
        }

        private void btnCycleBackward_Click(object sender, EventArgs e)
        {
            mf.bnd.bndList[0].hdLine?.Clear();

            if (mf.hdl.tracksArr.Count > 0)
            {
                mf.hdl.idx--;
                if (mf.hdl.idx < 0 ) mf.hdl.idx =(mf.hdl.tracksArr.Count - 1);
            }
            else
            {
                mf.hdl.idx = -1;
            }

            FixLabelsCurve();
        }

        private void btnDeleteCurve_Click(object sender, EventArgs e)
        {
            //mf.bnd.bndList[0].hdLine?.Clear();

            if (mf.hdl.tracksArr.Count > 0 && mf.hdl.idx > -1)
            {
                mf.hdl.tracksArr.RemoveAt(mf.hdl.idx);
                mf.hdl.idx--;
            }

            if (mf.hdl.tracksArr.Count > 0)
            {
                if (mf.hdl.idx == -1)
                {
                    mf.hdl.idx++;
                }
            }
            else mf.hdl.idx = -1;

            FixLabelsCurve();
        }

        private void oglSelf_MouseDown(object sender, MouseEventArgs e)
        {
            Point ptt = oglSelf.PointToClient(Cursor.Position);

            //Convert to Origin in the center of window, 800 pixels
            fixPt.X = ptt.X - 350;
            fixPt.Y = (700 - ptt.Y - 350);
            vec3 plotPt = new vec3
            {
                //convert screen coordinates to field coordinates
                easting = fixPt.X * mf.maxFieldDistance / 632.0,
                northing = fixPt.Y * mf.maxFieldDistance / 632.0,
                heading = 0
            };

            plotPt.easting += mf.fieldCenterX;
            plotPt.northing += mf.fieldCenterY;

            pint.easting = plotPt.easting;
            pint.northing = plotPt.northing;

            if (isA)
            {
                double minDistA = double.MaxValue;
                start = 99999; end = 99999;

                for (int j = 0; j < mf.bnd.bndList.Count; j++)
                {
                    for (int i = 0; i < mf.bnd.bndList[j].fenceLine.Count; i++)
                    {
                        double dist = ((pint.easting - mf.bnd.bndList[j].fenceLine[i].easting) * (pint.easting - mf.bnd.bndList[j].fenceLine[i].easting))
                                        + ((pint.northing - mf.bnd.bndList[j].fenceLine[i].northing) * (pint.northing - mf.bnd.bndList[j].fenceLine[i].northing));
                        if (dist < minDistA)
                        {
                            minDistA = dist;
                            bndSelect = j;
                            start = i;
                        }
                    }
                }

                isA = false;
            }
            else
            {
                double minDistA = double.MaxValue;
                int j = bndSelect;

                for (int i = 0; i < mf.bnd.bndList[j].fenceLine.Count; i++)
                {
                    double dist = ((pint.easting - mf.bnd.bndList[j].fenceLine[i].easting) * (pint.easting - mf.bnd.bndList[j].fenceLine[i].easting))
                                    + ((pint.northing - mf.bnd.bndList[j].fenceLine[i].northing) * (pint.northing - mf.bnd.bndList[j].fenceLine[i].northing));
                    if (dist < minDistA)
                    {
                        minDistA = dist;
                        end = i;
                    }
                }

                isA = true;

                if (start == end)
                {
                    start = 99999; end = 99999;
                    mf.TimedMessageBox(2000, "Line Error", "Start Point = End Point ");
                    return;
                }

                //build the lines
                if (rbtnCurve.Checked)
                {
                    mf.hdl.tracksArr.Add(new CHeadPath());
                    mf.hdl.idx = mf.hdl.tracksArr.Count - 1;

                    bool isLoop = false;
                    int limit = end;


                    if ((Math.Abs(start - end)) > (mf.bnd.bndList[bndSelect].fenceLine.Count * 0.5))
                    {
                        if (start < end)
                        {
                            (start, end) = (end, start);
                        }

                        isLoop = true;
                        if (start < end)
                        {
                            limit = end;
                            end = 0;
                        }
                        else
                        {
                            limit = end;
                            end = mf.bnd.bndList[bndSelect].fenceLine.Count;
                        }
                    }

                    else
                    {
                        if (start > end)
                        {
                            (start, end) = (end, start);
                        }
                    }

                    mf.hdl.tracksArr[mf.hdl.idx].trackPts?.Clear();
                    vec3 pt3 = new vec3();

                    if (start < end)
                    {
                        for (int i = start; i <= end; i++)
                        {
                            //calculate the point inside the boundary
                            pt3 = mf.bnd.bndList[bndSelect].fenceLine[i];
                            mf.hdl.tracksArr[mf.hdl.idx].trackPts.Add(pt3);

                            if (isLoop && i == mf.bnd.bndList[bndSelect].fenceLine.Count - 1)
                            {
                                i = -1;
                                isLoop = false;
                                end = limit;
                            }
                        }
                    }
                    else
                    {
                        for (int i = start; i >= end; i--)
                        {
                            //calculate the point inside the boundary
                            pt3 = mf.bnd.bndList[bndSelect].fenceLine[i];
                            mf.hdl.tracksArr[mf.hdl.idx].trackPts.Add(pt3);

                            if (isLoop && i == 0)
                            {
                                i = mf.bnd.bndList[bndSelect].fenceLine.Count - 1;
                                isLoop = false;
                                end = limit;
                            }
                        }
                    }

                    //who knows which way it actually goes
                    mf.hdl.CalculateHeadings(ref mf.hdl.tracksArr[mf.hdl.idx].trackPts);

                    int ptCnt = mf.hdl.tracksArr[mf.hdl.idx].trackPts.Count - 1;

                    for (int i = 1; i < 30; i++)
                    {
                        vec3 pt = new vec3(mf.hdl.tracksArr[mf.hdl.idx].trackPts[ptCnt]);
                        pt.easting += (Math.Sin(pt.heading) * i);
                        pt.northing += (Math.Cos(pt.heading) * i);
                        mf.hdl.tracksArr[mf.hdl.idx].trackPts.Add(pt);
                    }

                    vec3 stat = new vec3(mf.hdl.tracksArr[mf.hdl.idx].trackPts[0]);

                    for (int i = 1; i < 30; i++)
                    {
                        vec3 pt = new vec3(stat);
                        pt.easting -= (Math.Sin(pt.heading) * i);
                        pt.northing -= (Math.Cos(pt.heading) * i);
                        mf.hdl.tracksArr[mf.hdl.idx].trackPts.Insert(0, pt);
                    }

                    //create a name
                    mf.hdl.tracksArr[mf.hdl.idx].name = mf.hdl.idx.ToString() + " Cu " + DateTime.Now.ToString("mm:ss", CultureInfo.InvariantCulture);

                    mf.hdl.tracksArr[mf.hdl.idx].moveDistance = 0;

                    mf.hdl.tracksArr[mf.hdl.idx].mode = (int)TrackMode.Curve;

                    mf.FileSaveHeadLines();

                    //update the arrays
                    start = 99999; end = 99999;

                    FixLabelsCurve();
                    btnExit.Focus();
                }

                else if (rbtnLine.Checked)
                {
                    if ((Math.Abs(start - end)) > (mf.bnd.bndList[bndSelect].fenceLine.Count * 0.5))
                    {
                        if (start < end)
                        {
                            (start, end) = (end, start);
                        }
                    }

                    else
                    {
                        if (start > end)
                        {
                            (start, end) = (end, start);
                        }
                    }

                    vec3 ptA = new vec3(mf.bnd.bndList[bndSelect].fenceLine[start]);
                    vec3 ptB = new vec3(mf.bnd.bndList[bndSelect].fenceLine[end]);

                    //calculate the AB Heading
                    double abHead = Math.Atan2(
                        mf.bnd.bndList[bndSelect].fenceLine[end].easting - mf.bnd.bndList[bndSelect].fenceLine[start].easting,
                        mf.bnd.bndList[bndSelect].fenceLine[end].northing - mf.bnd.bndList[bndSelect].fenceLine[start].northing);
                    if (abHead < 0) abHead += glm.twoPI;

                    if (mf.hdl.idx < mf.hdl.tracksArr.Count - 1)
                    {
                        mf.hdl.idx++;
                        mf.hdl.tracksArr.Insert(mf.hdl.idx, new CHeadPath());
                    }
                    else
                    {
                        mf.hdl.tracksArr.Add(new CHeadPath());
                        mf.hdl.idx = mf.hdl.tracksArr.Count - 1;
                    }

                    mf.hdl.tracksArr[mf.hdl.idx].trackPts?.Clear();

                    ptA.heading = abHead;
                    ptB.heading = abHead;

                    for (int i = 0; i <= (int)(glm.Distance(ptA, ptB)); i++)
                    {
                        vec3 ptC = new vec3(ptA);
                        ptC.easting = (Math.Sin(abHead) * i) + ptA.easting;
                        ptC.northing = (Math.Cos(abHead) * i) + ptA.northing;
                        ptC.heading = abHead;
                        mf.hdl.tracksArr[mf.hdl.idx].trackPts.Add(ptC);
                    }

                    int ptCnt = mf.hdl.tracksArr[mf.hdl.idx].trackPts.Count - 1;

                    for (int i = 1; i < 10; i++)
                    {
                        vec3 pt = new vec3(mf.hdl.tracksArr[mf.hdl.idx].trackPts[ptCnt]);
                        pt.easting += (Math.Sin(pt.heading) * i);
                        pt.northing += (Math.Cos(pt.heading) * i);
                        mf.hdl.tracksArr[mf.hdl.idx].trackPts.Add(pt);
                    }

                    vec3 stat = new vec3(mf.hdl.tracksArr[mf.hdl.idx].trackPts[0]);

                    for (int i = 1; i < 10; i++)
                    {
                        vec3 pt = new vec3(stat);
                        pt.easting -= (Math.Sin(pt.heading) * i);
                        pt.northing -= (Math.Cos(pt.heading) * i);
                        mf.hdl.tracksArr[mf.hdl.idx].trackPts.Insert(0, pt);
                    }

                    //create a name
                    mf.hdl.tracksArr[mf.hdl.idx].name = mf.hdl.idx.ToString() + " AB " + DateTime.Now.ToString("hh:mm:ss", CultureInfo.InvariantCulture);

                    mf.hdl.tracksArr[mf.hdl.idx].moveDistance = 0;

                    mf.hdl.tracksArr[mf.hdl.idx].mode = (int)TrackMode.AB;

                    mf.FileSaveHeadLines();

                    FixLabelsCurve();
                    start = 99999; end = 99999;
                }

                btnSetLineDistance.PerformClick();
            }
        }

        private void oglSelf_Paint(object sender, PaintEventArgs e)
        {
            oglSelf.MakeCurrent();

            GL.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.ColorBufferBit);
            GL.LoadIdentity();                  // Reset The View

            //back the camera up
            GL.Translate(0, 0, -mf.maxFieldDistance);

            //translate to that spot in the world
            GL.Translate(-mf.fieldCenterX, -mf.fieldCenterY, 0);

            GL.Color3(1, 1, 1);

            //GL.Enable(EnableCap.Blend);

            //draw all the boundaries

            GL.LineWidth(2);

            for (int j = 0; j < mf.bnd.bndList.Count; j++)
            {
                if (j == bndSelect)
                    GL.Color3(0.8f, 0.8f, 0.8f);
                else
                    GL.Color3(0.50f, 0.25f, 0.10f);

                GL.Begin(PrimitiveType.Lines);
                for (int i = 0; i < mf.bnd.bndList[j].fenceLine.Count; i++)
                {
                    GL.Vertex3(mf.bnd.bndList[j].fenceLine[i].easting, mf.bnd.bndList[j].fenceLine[i].northing, 0);
                }
                GL.End();
            }

            //the vehicle
            //GL.PointSize(8.0f);
            //GL.Begin(PrimitiveType.Points);
            //GL.Color3(0.95f, 0.90f, 0.0f);
            //GL.Vertex3(mf.pivotAxlePos.easting, mf.pivotAxlePos.northing, 0.0);
            //GL.End();

            //draw the line building graphics
            //if (start != 99999 || end != 99999) 
                DrawABTouchLine();

            //draw the actual built lines
            //if (start == 99999 && end == 99999)
            {
                DrawBuiltLines();
            }

            GL.Disable(EnableCap.Blend);


            GL.Flush();
            oglSelf.SwapBuffers();
        }

        private void DrawBuiltLines()
        {

            GL.LineWidth(4);
            GL.Color3(0.93f, 0.599f, 0.50f);
            GL.Begin(PrimitiveType.LineStrip);

            for (int i = 0; i < mf.bnd.bndList[0].hdLine.Count; i++)
            {
                GL.Vertex3(mf.bnd.bndList[0].hdLine[i].easting, mf.bnd.bndList[0].hdLine[i].northing, 0);
            }
            GL.End();

            if (isLinesVisible && mf.hdl.tracksArr.Count > 0)
            {
                //GL.Enable(EnableCap.LineStipple);
                GL.LineStipple(1, 0x7070);
                GL.PointSize(4);

                for (int i = 0; i < mf.hdl.tracksArr.Count; i++)
                {
                    if (mf.hdl.tracksArr[i].mode == (int)TrackMode.AB)
                    {
                        GL.Color3(0.973f, 0.9f, 0.10f);
                    }
                    else
                    {
                        GL.Color3(0.3f, 0.99f, 0.20f);
                    }

                    GL.Begin(PrimitiveType.Points);
                    foreach (vec3 item in mf.hdl.tracksArr[i].trackPts)
                    {
                        GL.Vertex3(item.easting, item.northing, 0);
                    }
                    GL.End();
                }

                //GL.Disable(EnableCap.LineStipple);

                if (mf.hdl.idx > -1)
                {
                    GL.LineWidth(6);
                    GL.Color3(1.0f, 0.0f, 1.0f);

                    GL.Begin(PrimitiveType.LineStrip);
                    foreach (vec3 item in mf.hdl.tracksArr[mf.hdl.idx].trackPts)
                    {
                        GL.Vertex3(item.easting, item.northing, 0);
                    }
                    GL.End();

                    int cnt = mf.hdl.tracksArr[mf.hdl.idx].trackPts.Count - 1;
                    GL.PointSize(12);
                    GL.Color3(1.0f, 0.6f, 0.3f);
                    GL.Begin(PrimitiveType.Points);
                    GL.Vertex3(mf.hdl.tracksArr[mf.hdl.idx].trackPts[0].easting, mf.hdl.tracksArr[mf.hdl.idx].trackPts[0].northing, 0);
                    GL.Color3(0.3f, 0.3f, 0.99f);
                    GL.Vertex3(mf.hdl.tracksArr[mf.hdl.idx].trackPts[cnt].easting, mf.hdl.tracksArr[mf.hdl.idx].trackPts[cnt].northing, 0);
                    GL.End();


                    lblMovedDistance.Text = (mf.hdl.tracksArr[mf.hdl.idx].moveDistance*mf.m2FtOrM).ToString("N1");
                }
            }
        }

        private void DrawABTouchLine()
        {
            GL.Color3(0.65, 0.650, 0.0);
            GL.PointSize(16);
            GL.Begin(PrimitiveType.Points);

            GL.Color3(0, 0, 0);
            if (start != 99999) GL.Vertex3(mf.bnd.bndList[bndSelect].fenceLine[start].easting, mf.bnd.bndList[bndSelect].fenceLine[start].northing, 0);
            if (end != 99999) GL.Vertex3(mf.bnd.bndList[bndSelect].fenceLine[end].easting, mf.bnd.bndList[bndSelect].fenceLine[end].northing, 0);
            GL.End();

            GL.PointSize(10);
            GL.Begin(PrimitiveType.Points);

            GL.Color3(1.0f, 0.75f, 0.350f);
            if (start != 99999) GL.Vertex3(mf.bnd.bndList[bndSelect].fenceLine[start].easting, mf.bnd.bndList[bndSelect].fenceLine[start].northing, 0);

            GL.Color3(0.5f, 0.5f, 1.0f);
            if (end != 99999) GL.Vertex3(mf.bnd.bndList[bndSelect].fenceLine[end].easting, mf.bnd.bndList[bndSelect].fenceLine[end].northing, 0);
            GL.End();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            oglSelf.Refresh();
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            mf.FileSaveHeadLines();
            Close();
        }

        private void btnALength_Click(object sender, EventArgs e)
        {
            if (mf.hdl.idx > -1)
            {
                //and the beginning
                vec3 start = new vec3(mf.hdl.tracksArr[mf.hdl.idx].trackPts[0]);

                for (int i = 1; i < 10; i++)
                {
                    vec3 pt = new vec3(start);
                    pt.easting -= (Math.Sin(pt.heading) * i);
                    pt.northing -= (Math.Cos(pt.heading) * i);
                    mf.hdl.tracksArr[mf.hdl.idx].trackPts.Insert(0, pt);
                }
            }
        }

        private void btnBLength_Click(object sender, EventArgs e)
        {
            if (mf.hdl.idx > -1)
            {
                int ptCnt = mf.hdl.tracksArr[mf.hdl.idx].trackPts.Count - 1;

                for (int i = 1; i < 10; i++)
                {
                    vec3 pt = new vec3(mf.hdl.tracksArr[mf.hdl.idx].trackPts[ptCnt]);
                    pt.easting += (Math.Sin(pt.heading) * i);
                    pt.northing += (Math.Cos(pt.heading) * i);
                    mf.hdl.tracksArr[mf.hdl.idx].trackPts.Add(pt);
                }
            }
        }

        private void oglSelf_Resize(object sender, EventArgs e)
        {
            oglSelf.MakeCurrent();
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();

            //58 degrees view
            Matrix4 mat = Matrix4.CreatePerspectiveFieldOfView(1.01f, 1.0f, 1.0f, 20000);
            GL.LoadMatrix(ref mat);

            GL.MatrixMode(MatrixMode.Modelview);
        }

        private void btnSetLineDistance_Click(object sender, EventArgs e)
        {
            //mf.bnd.bndList[0].hdLine?.Clear();
            mf.hdl.desList?.Clear();

            if (mf.hdl.tracksArr.Count < 1 || mf.hdl.idx == -1) return;

            double distAway = (double)nudSetDistance.Value * mf.ftOrMtoM;
            mf.hdl.tracksArr[mf.hdl.idx].moveDistance += distAway;

            double distSqAway = (distAway * distAway) - 0.01;
            vec3 point;

            int refCount = mf.hdl.tracksArr[mf.hdl.idx].trackPts.Count;
            for (int i = 0; i < refCount; i++)
            {
                point = new vec3(
                mf.hdl.tracksArr[mf.hdl.idx].trackPts[i].easting - (Math.Sin(glm.PIBy2 + mf.hdl.tracksArr[mf.hdl.idx].trackPts[i].heading) * distAway),
                mf.hdl.tracksArr[mf.hdl.idx].trackPts[i].northing - (Math.Cos(glm.PIBy2 + mf.hdl.tracksArr[mf.hdl.idx].trackPts[i].heading) * distAway),
                mf.hdl.tracksArr[mf.hdl.idx].trackPts[i].heading);
                bool Add = true;

                for (int t = 0; t < refCount; t++)
                {
                    double dist = ((point.easting - mf.hdl.tracksArr[mf.hdl.idx].trackPts[t].easting) * (point.easting - mf.hdl.tracksArr[mf.hdl.idx].trackPts[t].easting))
                        + ((point.northing - mf.hdl.tracksArr[mf.hdl.idx].trackPts[t].northing) * (point.northing - mf.hdl.tracksArr[mf.hdl.idx].trackPts[t].northing));
                    if (dist < distSqAway)
                    {
                        Add = false;
                        break;
                    }
                }

                if (Add)
                {
                    if (mf.hdl.desList.Count > 0)
                    {
                        double dist = ((point.easting - mf.hdl.desList[mf.hdl.desList.Count - 1].easting) * (point.easting - mf.hdl.desList[mf.hdl.desList.Count - 1].easting))
                            + ((point.northing - mf.hdl.desList[mf.hdl.desList.Count - 1].northing) * (point.northing - mf.hdl.desList[mf.hdl.desList.Count - 1].northing));
                        if (dist > 1)
                            mf.hdl.desList.Add(point);
                    }
                    else mf.hdl.desList.Add(point);
                }
            }

            mf.hdl.tracksArr[mf.hdl.idx].trackPts.Clear();

            for (int i = 0; i < mf.hdl.desList.Count; i++)
            {                
                mf.hdl.tracksArr[mf.hdl.idx].trackPts.Add(new vec3(mf.hdl.desList[i]));
            }

            mf.hdl.desList?.Clear();
        }

        private void nudSetDistance_Click(object sender, EventArgs e)
        {
            mf.KeypadToNUD((NumericUpDown)sender, this);
            btnExit.Focus();
        }

        // Returns 1 if the lines intersect, otherwis
        public double iE = 0, iN = 0;
        public List<int> crossings = new List<int>(1);

        private void btnDeleteHeadland_Click(object sender, EventArgs e)
        {
            start = 99999; end = 99999;
            isA = true;
            mf.hdl.desList?.Clear();
            mf.hdl.sliceArr?.Clear();
            mf.hdl.backupList?.Clear();
            mf.bnd.bndList[0].hdLine?.Clear();

            //int ptCount = mf.bnd.bndList[0].fenceLine.Count;

            //for (int i = 0; i < ptCount; i++)
            //{
            //    mf.bnd.bndList[0].hdLine.Add(new vec3(mf.bnd.bndList[0].fenceLine[i]));
            //}
        }

        private void btnBndLoop_Click(object sender, EventArgs e)
        {
            mf.bnd.bndList[0].hdLine?.Clear();

            int numOfLines = mf.hdl.tracksArr.Count;
            int nextLine = 0;
            crossings.Clear();

            int isStart = 0;

            for (int lineNum = 0; lineNum < mf.hdl.tracksArr.Count; lineNum++)
            {
                nextLine = lineNum - 1;
                if (nextLine < 0) nextLine = mf.hdl.tracksArr.Count - 1;

                if (nextLine == lineNum)
                {
                    mf.TimedMessageBox(2000, "Create Error", "Is there maybe only 1 line?");
                    return;
                }

                for (int i = 0; i < mf.hdl.tracksArr[lineNum].trackPts.Count - 2; i++)
                {

                    for (int k = 0; k < mf.hdl.tracksArr[nextLine].trackPts.Count - 2; k++)
                    {
                        int res = GetLineIntersection(
                        mf.hdl.tracksArr[lineNum].trackPts[i].easting,
                        mf.hdl.tracksArr[lineNum].trackPts[i].northing,
                        mf.hdl.tracksArr[lineNum].trackPts[i + 1].easting,
                        mf.hdl.tracksArr[lineNum].trackPts[i + 1].northing,

                        mf.hdl.tracksArr[nextLine].trackPts[k].easting,
                        mf.hdl.tracksArr[nextLine].trackPts[k].northing,
                        mf.hdl.tracksArr[nextLine].trackPts[k + 1].easting,
                        mf.hdl.tracksArr[nextLine].trackPts[k + 1].northing,
                        ref iE, ref iN);
                        if (res == 1)
                        {
                            if (isStart == 0) i++;
                            crossings.Add(i);
                            isStart++;
                            if (isStart == 2) goto again;
                            nextLine = lineNum + 1;

                            if (nextLine > mf.hdl.tracksArr.Count - 1) nextLine = 0;
                        }
                    }
                }

            again:
                isStart = 0;
            }

            if (crossings.Count != mf.hdl.tracksArr.Count * 2)
            {
                mf.TimedMessageBox(2000, "Crosings Error", "Make sure all ends cross only once");
                mf.bnd.bndList[0].hdLine?.Clear();
                return;
            }

            for (int i = 0; i < mf.hdl.tracksArr.Count; i++)
            {
                int low = crossings[i * 2];
                int high = crossings[i * 2 + 1];
                for (int k = low; k < high; k++)
                {
                    mf.bnd.bndList[0].hdLine.Add(mf.hdl.tracksArr[i].trackPts[k]);
                }
            }

            //for (int i = 0; i < mf.hdl.tracksArr.Count; i++)
            //{
            //    mf.hdl.desList?.Clear();

            //    int low = crossings[i * 2];
            //    int high = crossings[i * 2 + 1];
            //    for (int k = low; k < high; k++)
            //    {
            //        mf.hdl.desList.Add(mf.hdl.tracksArr[i].trackPts[k]);
            //    }

            //    mf.hdl.tracksArr[i].trackPts?.Clear();

            //    foreach (var item in mf.hdl.desList)
            //    {
            //        mf.hdl.tracksArr[i].trackPts.Add(new vec3(item));
            //    }
            //}

            vec3[] hdArr;

            if (mf.bnd.bndList[0].hdLine.Count > 0)
            {
                hdArr = new vec3[mf.bnd.bndList[0].hdLine.Count];
                mf.bnd.bndList[0].hdLine.CopyTo(hdArr);
                mf.bnd.bndList[0].hdLine?.Clear();
            }
            else
            {
                mf.bnd.bndList[0].hdLine?.Clear();
                return;
            }

            //does headland control sections
            mf.bnd.isSectionControlledByHeadland = cboxIsSectionControlled.Checked;
            Properties.Settings.Default.setHeadland_isSectionControlled = cboxIsSectionControlled.Checked;
            Properties.Settings.Default.Save();

            //middle points
            for (int i = 1; i < hdArr.Length; i++)
            {
                hdArr[i - 1].heading = Math.Atan2(hdArr[i - 1].easting - hdArr[i].easting, hdArr[i - 1].northing - hdArr[i].northing);
                if (hdArr[i].heading < 0) hdArr[i].heading += glm.twoPI;
            }

            double delta = 0;
            for (int i = 0; i < hdArr.Length; i++)
            {
                if (i == 0)
                {
                    mf.bnd.bndList[0].hdLine.Add(new vec3(hdArr[i].easting, hdArr[i].northing, hdArr[i].heading));
                    continue;
                }
                delta += (hdArr[i - 1].heading - hdArr[i].heading);

                if (Math.Abs(delta) > 0.01)
                {
                    vec3 pt = new vec3(hdArr[i].easting, hdArr[i].northing, hdArr[i].heading);

                    mf.bnd.bndList[0].hdLine.Add(pt);
                    delta = 0;
                }
            }
            mf.FileSaveHeadland();
        }

        private void btnDeletePoints_Click(object sender, EventArgs e)
        {
            start = 99999; end = 99999;
            isA = true;
        }

        private void cboxToolWidths_SelectedIndexChanged(object sender, EventArgs e)
        {
            nudSetDistance.Value = (decimal)(Math.Round((mf.tool.width-mf.tool.overlap) * cboxToolWidths.SelectedIndex, 1));
        }

        public int GetLineIntersection(double p0x, double p0y, double p1x, double p1y,
        double p2x, double p2y, double p3x, double p3y, ref double iEast, ref double iNorth)
        {
            double s1x, s1y, s2x, s2y;
            s1x = p1x - p0x;
            s1y = p1y - p0y;

            s2x = p3x - p2x;
            s2y = p3y - p2y;

            double s, t;
            s = (-s1y * (p0x - p2x) + s1x * (p0y - p2y)) / (-s2x * s1y + s1x * s2y);

            if (s >= 0 && s <= 1)
            {
                //check oher side
                t = (s2x * (p0y - p2y) - s2y * (p0x - p2x)) / (-s2x * s1y + s1x * s2y);
                if (t >= 0 && t <= 1)
                {
                    // Collision detected
                    iEast = p0x + (t * s1x);
                    iNorth = p0y + (t * s1y);
                    return 1;
                }
            }

            return 0; // No collision
        }


        private void oglSelf_Load(object sender, EventArgs e)
        {
            oglSelf.MakeCurrent();
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(CullFaceMode.Back);
            GL.ClearColor(0.22f, 0.22f, 0.22f, 1.0f);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
        }
    }
}