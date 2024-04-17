using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Hanzzz.MeshSlicerFree
{

public static class Triangulation
{
    public const double EPSILON = 0.0000000001;
    private static bool DoubleEqual(double a, double b)
    {
        return Math.Abs(a-b) <= EPSILON;
    }
    private struct Point
    {
        public double x, y;

        public bool GreaterThan(Point other)
        {
            if(this.y > other.y+EPSILON)
            {
                return true;
            }
            else if(this.y < other.y-EPSILON)
            {
                return false;
            }
            else
            {
                return this.x > other.x;
            }
        }
        public bool LessThan(Point other)
        {
            if(this.y < other.y-EPSILON)
            {
                return true;
            }
            else if(this.y > other.y+EPSILON)
            {
                return false;
            }
            else
            {
                return this.x < other.x;
            }
        }
        public bool GreaterThanEqualTo(Point other)
        {
            if(this.y > other.y+EPSILON)
            {
                return true;
            }
            if(this.y < other.y-EPSILON)
            {
                return false;
            }
            return this.x >= other.x;
        }
        public bool EqualTo(Point other)
        {
            return DoubleEqual(this.y, other.y) && DoubleEqual(this.x, other.x);
        }

        public static Point PositiveInfinity = new Point {x=Double.PositiveInfinity, y=Double.PositiveInfinity};
        public static Point NegativeInfinity = new Point {x=Double.NegativeInfinity, y=Double.NegativeInfinity};
    }
    private struct Segment
    {
        public Point point0, point1;
        public int root0Index, root1Index;
        public int nextSegmentIndex;
        public int prevSegmentIndex;
        public bool isInserted;
    }

    private struct Trapezoid
    {
        public int leftSegmentIndex, rightSegmentIndex;
        public Point topPoint, lowPoint;
        public int topTrapezoid0Index, topTrapezoid1Index;
        public int topTrapezoid3Index;
        public Side topTrapezoid3Side;
        public int lowTrapezoid0Index, lowTrapezoid1Index;

        public int nodeIndex; // for the trapezoid corresponding to itself
        public State trapezoidState;
    }
    public enum Side
    {
        LEFT = 0,
        RIGHT
    }
    public enum State
    {
        VALID = 0,
        INVALID
    }

    private struct Node
    {
        public NodeType nodeType;
        public Point point;
        public int segmentIndex;
        public int trapezoidIndex;

        public int parentNodeIndex, leftNodeIndex, rightNodeIndex;
    }
    public enum NodeType
    {
        VERTEX = 0,
        SEGMENT,
        TRAPEZOID
    }

    private struct MonotoneChain
    {
        public int vertexNumber;
        public int next;
        public int prev;
        public bool marked;
    }

    private struct VertexChain
    {
        public Point point;
        public int[] vertexNext;
        public int[] vertexPosition;
        public int nextfree;

        public VertexChain(int _)
        {
            point = new Point{x=0.0,y=0.0};
            vertexNext = new int[4];
            vertexPosition = new int[4];
            nextfree = 0;
        }
    }

    private static int segmentCount;

    private static Segment[] segments;
    private static Node[] nodes;
    private static Trapezoid[] trapezoids;
    private static int nodeAddIndex, trapezoidAddIndex;

    private static int[] segmentIndexPermutation;
    private static int segmentIndexPermutationIndex;


    private static MonotoneChain[] monotoneChain;
    private static VertexChain[] vertexChain;
    private static int[] monotoneIndex;
    private static bool[] visited;
    private static int monotoneChainIndex, op_idx, monotoneIndexIndex;

    private static int[,] res;
    private static int resCount;

    public static (int, int[,]) Triangulate(List<List<Vector2>> points)
    {
        // prepare data
        segmentCount = points.Sum(x=>x.Count);
        segments = new Segment[segmentCount+1];
        nodes = new Node[8*(segmentCount+1)];
        trapezoids = new Trapezoid[4*(segmentCount+1)];
        nodeAddIndex = 1;
        trapezoidAddIndex = 1;
        InitializeSegmentIndexPermutation(segmentCount);

        int index = 1;
        for(int i=0; i<points.Count; i++)
        {
            for(int j=0; j<points[i].Count; j++)
            {
                segments[index+j].point0.x = points[i][j].x;
                segments[index+j].point0.y = points[i][j].y;
                segments[index+j].isInserted = false;

                if(0 == j)
                {
                    segments[index+j].nextSegmentIndex = index+j+1;
                    segments[index+j].prevSegmentIndex = index + points[i].Count-1;
                    segments[index + points[i].Count-1].point1 = segments[index+j].point0;
                }
                else if(points[i].Count-1 == j)
                {
                    segments[index+j].nextSegmentIndex = index;
                    segments[index+j].prevSegmentIndex = index+j-1;
                    segments[index+j-1].point1 = segments[index+j].point0;
                }
                else
                {
                    segments[index+j].nextSegmentIndex = index+j+1;
                    segments[index+j].prevSegmentIndex = index+j-1;
                    segments[index+j-1].point1 = segments[index+j].point0;
                }
            }
            index += points[i].Count;
        }
        // trapezoidation

        int rootIndex = AddFirstSegment(GetSegmentIndex());
        for(int i=1; i<=segmentCount; i++)
        {
            segments[i].root0Index = segments[i].root1Index = rootIndex;
        }

        for(int i=1; i<=MathLogStar(segmentCount); i++)
        {
            for(int j=MathN(segmentCount, i-1)+1; j <= MathN(segmentCount, i); j++)
            {
                AddSegment(GetSegmentIndex());
            }
            for(int j=1; j<=segmentCount; j++)
            {
                FindNewRoot(j);
            }
        }
        for(int i=MathN(segmentCount, MathLogStar(segmentCount))+1; i<=segmentCount; i++)
        {
            AddSegment(GetSegmentIndex());
        }

        // monotonate trapezoids

        monotoneChain = new MonotoneChain[4*(segmentCount+1)];
        vertexChain = new VertexChain[(segmentCount+1)];
        for(int i=0; i<vertexChain.Length; i++)
        {
            vertexChain[i] = new VertexChain(-1);
        }
        monotoneIndex = new int[(segmentCount+1)];
        visited = new bool[4*(segmentCount+1)];

        int tstart = 0;
        for(int i=1; i<4*(segmentCount+1); i++)
        {
            if(InsidePolygon(i))
            {
                tstart = i;
                break;
            }
        }

        for(int i=1; i<=segmentCount; i++)
        {
            monotoneChain[i].prev = segments[i].prevSegmentIndex;
            monotoneChain[i].next = segments[i].nextSegmentIndex;
            monotoneChain[i].vertexNumber = i;
            
            vertexChain[i].point = segments[i].point0;
            vertexChain[i].vertexNext[0] = segments[i].nextSegmentIndex;
            vertexChain[i].vertexPosition[0] = i;
            vertexChain[i].nextfree = 1;
        }
        monotoneChainIndex = segmentCount+1;
        monotoneIndexIndex = 1;
        monotoneIndex[0] = 1;

        if(trapezoids[tstart].topTrapezoid0Index > 0)
        {
            TraversePolygon(0, tstart, trapezoids[tstart].topTrapezoid0Index, true);
        }
        else if(trapezoids[tstart].lowTrapezoid0Index > 0)
        {
            TraversePolygon(0, tstart, trapezoids[tstart].lowTrapezoid0Index, false);
        }

        int monotoneCount = GetNewMonotoneIndex();

        //for(int i = 0; i<monotoneCount; i++)
        //{
        //    string str = $"Polygon {i}: ";
        //    int vfirst = monotoneChain[monotoneIndex[i]].vertexNumber;
        //    int p = monotoneChain[monotoneIndex[i]].next;
        //    str+=monotoneChain[monotoneIndex[i]].vertexNumber;

        //    while(monotoneChain[p].vertexNumber!=vfirst)
        //    {
        //        str+=monotoneChain[p].vertexNumber;
        //        p=monotoneChain[p].next;
        //    }
        //    Debug.Log(str);
        //}

        // triangulate single polygon

        res = new int[segmentCount+1, 3];
        resCount = 0;

        TriangulateMonotonePolygon(segmentCount, monotoneCount);

        return (resCount, res);
    }


 //          t4
 //    --------------------
 //   		  \
 //   	t1	   \     t2
 //   		    \
 //    --------------------
 //              t3
    private static int AddFirstSegment(int segmentIndex)
    {
        int i1, i2, i3, i4, i5, i6, i7, root;
        int t1, t2, t3, t4;

        i1 = GetNewNodeIndex();
        i2 = GetNewNodeIndex();
        i3 = GetNewNodeIndex();
        i4 = GetNewNodeIndex();
        i5 = GetNewNodeIndex();
        i6 = GetNewNodeIndex();
        i7 = GetNewNodeIndex();
        root = i1;
        t1 = GetNewTrapezoidIndex();
        t2 = GetNewTrapezoidIndex();
        t3 = GetNewTrapezoidIndex();
        t4 = GetNewTrapezoidIndex();


        nodes[i1].nodeType = NodeType.VERTEX;
        nodes[i1].point = segments[segmentIndex].point0.GreaterThan(segments[segmentIndex].point1) ? segments[segmentIndex].point0 : segments[segmentIndex].point1;
        nodes[i1].rightNodeIndex = i2;
        nodes[i1].leftNodeIndex = i3;

        nodes[i2].nodeType = NodeType.TRAPEZOID;
        nodes[i2].parentNodeIndex = i1;

        nodes[i3].nodeType = NodeType.VERTEX;
        nodes[i3].point = segments[segmentIndex].point0.GreaterThan(segments[segmentIndex].point1) ? segments[segmentIndex].point1 : segments[segmentIndex].point0;
        nodes[i3].parentNodeIndex = i1;
        nodes[i3].leftNodeIndex = i4;
        nodes[i3].rightNodeIndex = i5;

        nodes[i4].nodeType = NodeType.TRAPEZOID;
        nodes[i4].parentNodeIndex = i3;

        nodes[i5].nodeType = NodeType.SEGMENT;
        nodes[i5].segmentIndex = segmentIndex;
        nodes[i5].parentNodeIndex = i3;
        nodes[i5].leftNodeIndex = i6;
        nodes[i5].rightNodeIndex = i7;

        nodes[i6].nodeType = NodeType.TRAPEZOID;
        nodes[i6].parentNodeIndex = i5;

        nodes[i7].nodeType = NodeType.TRAPEZOID;
        nodes[i7].parentNodeIndex = i5;


        trapezoids[t1].topPoint = trapezoids[t2].topPoint = trapezoids[t4].lowPoint = nodes[i1].point;
        trapezoids[t1].lowPoint = trapezoids[t2].lowPoint = trapezoids[t3].topPoint = nodes[i3].point;
        trapezoids[t4].topPoint = Point.PositiveInfinity;
        trapezoids[t3].lowPoint = Point.NegativeInfinity;

        trapezoids[t1].rightSegmentIndex = trapezoids[t2].leftSegmentIndex = segmentIndex;

        trapezoids[t1].topTrapezoid0Index = trapezoids[t2].topTrapezoid0Index = t4;
        trapezoids[t1].lowTrapezoid0Index = trapezoids[t2].lowTrapezoid0Index = t3;
        trapezoids[t4].lowTrapezoid0Index = trapezoids[t3].topTrapezoid0Index = t1;
        trapezoids[t4].lowTrapezoid1Index = trapezoids[t3].topTrapezoid1Index = t2;

        trapezoids[t1].nodeIndex = i6;
        trapezoids[t2].nodeIndex = i7;
        trapezoids[t3].nodeIndex = i4;
        trapezoids[t4].nodeIndex = i2;

        nodes[i6].trapezoidIndex = t1;
        nodes[i7].trapezoidIndex = t2;
        nodes[i4].trapezoidIndex = t3;
        nodes[i2].trapezoidIndex = t4;

        segments[segmentIndex].isInserted = true;
        return root;
    }

    private static void AddSegment(int segmentIndex)
    {
        int tfirst, tlast;
        int tfirstr = 0;
        int tlastr = 0;
        bool isTriangleBottom = false;
        bool isSwapped = false;

        if(segments[segmentIndex].point1.GreaterThan(segments[segmentIndex].point0))
        {
            Point tempPoint;
            int tempIndex;
            tempPoint = segments[segmentIndex].point0;
            segments[segmentIndex].point0 = segments[segmentIndex].point1;
            segments[segmentIndex].point1 = tempPoint;
            tempIndex = segments[segmentIndex].root0Index;
            segments[segmentIndex].root0Index = segments[segmentIndex].root1Index;
            segments[segmentIndex].root1Index = tempIndex;
            isSwapped = true;
        }

        // top point
        if(!IsInserted(segmentIndex, isSwapped))
        {
            tfirst = AddSegmentAddPoint(segmentIndex, true);
        }
        else
        {
            tfirst = FindTrapezoid(segments[segmentIndex].point0, segments[segmentIndex].point1, segments[segmentIndex].root0Index);
        }

        //low point
        if(!IsInserted(segmentIndex, !isSwapped))
        {
            tlast = AddSegmentAddPoint(segmentIndex, false);
        }
        else
        {
            tlast = FindTrapezoid(segments[segmentIndex].point1, segments[segmentIndex].point0, segments[segmentIndex].root1Index);
            isTriangleBottom = true;
        }

        int tcurrent = tfirst;

        while(tcurrent > 0 && trapezoids[tcurrent].lowPoint.GreaterThanEqualTo(trapezoids[tlast].lowPoint))
        {
            int tcurrenSave, tnSave;
            int tempIndex;
            int i1, i2;
            int tn;

            tempIndex = trapezoids[tcurrent].nodeIndex;
            i1 = GetNewNodeIndex();
            i2 = GetNewNodeIndex();

            nodes[tempIndex].nodeType = NodeType.SEGMENT;
            nodes[tempIndex].segmentIndex = segmentIndex;
            nodes[tempIndex].leftNodeIndex = i1;
            nodes[tempIndex].rightNodeIndex = i2;

            nodes[i1].nodeType = NodeType.TRAPEZOID;
            nodes[i1].trapezoidIndex = tcurrent;
            nodes[i1].parentNodeIndex = tempIndex;

            nodes[i2].nodeType = NodeType.TRAPEZOID;
            nodes[i2].trapezoidIndex = tn = GetNewTrapezoidIndex();
            nodes[i2].parentNodeIndex = tempIndex;

            trapezoids[tn] = trapezoids[tcurrent];
            trapezoids[tcurrent].nodeIndex = i1;
            trapezoids[tn].nodeIndex = i2;
            tcurrenSave = tcurrent;
            tnSave = tn;

            if(tcurrent == tfirst)
            {
                tfirstr = tn;
            }
            if(trapezoids[tcurrent].lowPoint.EqualTo(trapezoids[tlast].lowPoint))
            {
                tlastr = tn;
            }

            if(trapezoids[tcurrent].lowTrapezoid0Index <= 0 && trapezoids[tcurrent].lowTrapezoid1Index <= 0)
            {
                Debug.LogError("Triangulation bug");
                throw new Exception();
            }
            else if(trapezoids[tcurrent].lowTrapezoid0Index > 0 && trapezoids[tcurrent].lowTrapezoid1Index <= 0)
            {
                tcurrent = AddSegmentOneTrapezoidBelow(tcurrent, tn, segmentIndex, tlast, isTriangleBottom, isSwapped, true);
            }
            else if(trapezoids[tcurrent].lowTrapezoid0Index <= 0 && trapezoids[tcurrent].lowTrapezoid1Index > 0)
            {
                tcurrent = AddSegmentOneTrapezoidBelow(tcurrent, tn, segmentIndex, tlast, isTriangleBottom, isSwapped, false);
            }
            else
            {
                double y0, ty;
                Point tempPoint;
                bool intersectLowTrapezoid0 = false;

                if(DoubleEqual(trapezoids[tcurrent].lowPoint.y, segments[segmentIndex].point0.y))
                {
                    if(trapezoids[tcurrent].lowPoint.x > segments[segmentIndex].point0.x)
                    {
                        intersectLowTrapezoid0 = true;
                    }
                }
                else
                {
                    tempPoint.y = y0 = trapezoids[tcurrent].lowPoint.y;
                    ty = (y0-segments[segmentIndex].point0.y) / (segments[segmentIndex].point1.y-segments[segmentIndex].point0.y);
                    tempPoint.x = segments[segmentIndex].point0.x + ty*(segments[segmentIndex].point1.x-segments[segmentIndex].point0.x);

                    if(tempPoint.LessThan(trapezoids[tcurrent].lowPoint))
                    {
                        intersectLowTrapezoid0 = true;
                    }
                }

                AddSegmentHandleTopTrapezoid(tcurrent, tn, segmentIndex);

                if(isTriangleBottom && trapezoids[tcurrent].lowPoint.EqualTo(trapezoids[tlast].lowPoint))
                {
                    trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid0Index = tcurrent;
                    trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid1Index = -1;
                    trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid0Index = tn;
                    trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid1Index = -1;

                    trapezoids[tn].lowTrapezoid0Index = trapezoids[tcurrent].lowTrapezoid1Index;
                    trapezoids[tcurrent].lowTrapezoid1Index = trapezoids[tn].lowTrapezoid1Index = -1;

                    tcurrent = trapezoids[tcurrent].lowTrapezoid1Index;
                }
                else if(intersectLowTrapezoid0)
                {
                    trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid0Index = tcurrent;
                    trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid1Index = tn;
                    trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid0Index = tn;
                    trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid1Index = -1;

                    trapezoids[tcurrent].lowTrapezoid1Index = -1;

                    tcurrent = trapezoids[tcurrent].lowTrapezoid0Index;
                }
                else
                {
                    trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid0Index = tcurrent;
                    trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid1Index = -1;
                    trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid0Index = tcurrent;
                    trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid1Index = tn;

                    trapezoids[tn].lowTrapezoid0Index = trapezoids[tcurrent].lowTrapezoid1Index;
                    trapezoids[tn].lowTrapezoid1Index = -1;

                    tcurrent = trapezoids[tcurrent].lowTrapezoid1Index;
                }
            }
            trapezoids[tcurrenSave].rightSegmentIndex = trapezoids[tnSave].leftSegmentIndex = segmentIndex;
        }

        if(isSwapped)
        {
            Point tempPoint;
            int tempIndex;
            tempPoint = segments[segmentIndex].point0;
            segments[segmentIndex].point0 = segments[segmentIndex].point1;
            segments[segmentIndex].point1 = tempPoint;
            tempIndex = segments[segmentIndex].root0Index;
            segments[segmentIndex].root0Index = segments[segmentIndex].root1Index;
            segments[segmentIndex].root1Index = tempIndex;
        }

        AddSegmentMergeTrapezoids(segmentIndex, tfirst, tlast, true);
        AddSegmentMergeTrapezoids(segmentIndex, tfirstr, tlastr, false);

        segments[segmentIndex].isInserted = true;
    }

    private static int AddSegmentAddPoint(int segmentIndex, bool addPoint0)
    {
        int tempIndex;
        int tt, tl;
        int i1, i2;

        if(addPoint0)
        {
            tt = FindTrapezoid(segments[segmentIndex].point0, segments[segmentIndex].point1, segments[segmentIndex].root0Index);
        }
        else
        {
            tt = FindTrapezoid(segments[segmentIndex].point1, segments[segmentIndex].point0, segments[segmentIndex].root1Index);
        }
        tl = GetNewTrapezoidIndex();

        trapezoids[tl] = trapezoids[tt];
        if(addPoint0)
        {
            trapezoids[tt].lowPoint = trapezoids[tl].topPoint = segments[segmentIndex].point0;
        }
        else
        {
            trapezoids[tt].lowPoint = trapezoids[tl].topPoint = segments[segmentIndex].point1;
        }

        trapezoids[tt].lowTrapezoid0Index = tl;
        trapezoids[tt].lowTrapezoid1Index = 0;
        trapezoids[tl].topTrapezoid0Index = tt;
        trapezoids[tl].topTrapezoid1Index = 0;

        if((tempIndex = trapezoids[tl].lowTrapezoid0Index) > 0 && trapezoids[tempIndex].topTrapezoid0Index == tt)
        {
            trapezoids[tempIndex].topTrapezoid0Index = tl;
        }
        if((tempIndex = trapezoids[tl].lowTrapezoid0Index) > 0 && trapezoids[tempIndex].topTrapezoid1Index == tt)
        {
            trapezoids[tempIndex].topTrapezoid1Index = tl;
        }
        if((tempIndex = trapezoids[tl].lowTrapezoid1Index) > 0 && trapezoids[tempIndex].topTrapezoid0Index == tt)
        {
            trapezoids[tempIndex].topTrapezoid0Index = tl;
        }
        if((tempIndex = trapezoids[tl].lowTrapezoid1Index) > 0 && trapezoids[tempIndex].topTrapezoid1Index == tt)
        {
            trapezoids[tempIndex].topTrapezoid1Index = tl;
        }

        i1 = GetNewNodeIndex();
        i2 = GetNewNodeIndex();
        tempIndex = trapezoids[tt].nodeIndex;

        nodes[tempIndex].nodeType = NodeType.VERTEX;
        if(addPoint0)
        {
            nodes[tempIndex].point = segments[segmentIndex].point0;
        }
        else
        {
            nodes[tempIndex].point = segments[segmentIndex].point1;
        }
        nodes[tempIndex].segmentIndex = segmentIndex; // don't know why added this
        nodes[tempIndex].leftNodeIndex = i2;
        nodes[tempIndex].rightNodeIndex = i1;

        nodes[i1].nodeType = NodeType.TRAPEZOID;
        nodes[i1].trapezoidIndex = tt;
        nodes[i1].parentNodeIndex = tempIndex;
        trapezoids[tt].nodeIndex = i1;
            
        nodes[i2].nodeType = NodeType.TRAPEZOID;
        nodes[i2].trapezoidIndex = tl;
        nodes[i2].parentNodeIndex = tempIndex;
        trapezoids[tl].nodeIndex = i2;

        if(addPoint0)
        {
            return tl;
        }
        else
        {
            return tt;
        }    
    }

    private static int AddSegmentOneTrapezoidBelow(int tcurrent, int tn, int segmentIndex, int tlast, bool isTriangleBottom, bool isSwapped, bool useLowTrapezoid0Index)
    {
        AddSegmentHandleTopTrapezoid(tcurrent, tn, segmentIndex);

        if(useLowTrapezoid0Index)
        {
            if(isTriangleBottom && trapezoids[tcurrent].lowPoint.EqualTo(trapezoids[tlast].lowPoint))
            {
                int tempSegmentIndex;
                if(isSwapped)
                {
                    tempSegmentIndex = segments[segmentIndex].prevSegmentIndex;
                }
                else
                {
                    tempSegmentIndex = segments[segmentIndex].nextSegmentIndex;
                }

                if(tempSegmentIndex > 0 && IsLeftOf(segments[segmentIndex].point0, tempSegmentIndex))
                {
                    trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid0Index = tcurrent;
                    trapezoids[tn].lowTrapezoid0Index = trapezoids[tn].lowTrapezoid1Index = -1;
                }
                else
                {
                    trapezoids[trapezoids[tn].lowTrapezoid0Index].topTrapezoid1Index = tn;
                    trapezoids[tcurrent].lowTrapezoid0Index = trapezoids[tcurrent].lowTrapezoid1Index = -1;
                }
            }
            else
            {
                if(trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid0Index > 0 && trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid1Index > 0)
                {
                    if(tcurrent == trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid0Index)
                    {
                        trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid3Index = trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid1Index;
                        trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid3Side = Side.LEFT;
                    }
                    else
                    {
                        trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid3Index = trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid0Index;
                        trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid3Side = Side.RIGHT;
                    }
                }
                trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid0Index = tcurrent;
                trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid1Index = tn;
            }

            return trapezoids[tcurrent].lowTrapezoid0Index;
        }
        else
        {
            if(isTriangleBottom && trapezoids[tcurrent].lowPoint.EqualTo(trapezoids[tlast].lowPoint))
            {
                int tempSegmentIndex;
                if(isSwapped)
                {
                    tempSegmentIndex = segments[segmentIndex].prevSegmentIndex;
                }
                else
                {
                    tempSegmentIndex = segments[segmentIndex].nextSegmentIndex;
                }

                if(tempSegmentIndex > 0 && IsLeftOf(segments[segmentIndex].point0, tempSegmentIndex))
                {
                    trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid0Index = tcurrent;
                    trapezoids[tn].lowTrapezoid0Index = trapezoids[tn].lowTrapezoid1Index = -1;
                }
                else
                {
                    trapezoids[trapezoids[tn].lowTrapezoid1Index].topTrapezoid1Index = tn;
                    trapezoids[tcurrent].lowTrapezoid0Index = trapezoids[tcurrent].lowTrapezoid1Index = -1;
                }
            }
            else
            {
                if(trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid0Index > 0 && trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid1Index > 0)
                {
                    if(tcurrent == trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid0Index)
                    {
                        trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid3Index = trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid1Index;
                        trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid3Side = Side.LEFT;
                    }
                    else
                    {
                        trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid3Index = trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid0Index;
                        trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid3Side = Side.RIGHT;
                    }
                }
                trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid0Index = tcurrent;
                trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid1Index = tn;
            }

            return trapezoids[tcurrent].lowTrapezoid1Index;
        }
    }

    private static void AddSegmentHandleTopTrapezoid(int tcurrent, int tn, int segmentIndex)
    {
        if(trapezoids[tcurrent].topTrapezoid0Index > 0 && trapezoids[tcurrent].topTrapezoid1Index > 0)
        {
            if(trapezoids[tcurrent].topTrapezoid3Index > 0)
            {
                if(Side.LEFT == trapezoids[tcurrent].topTrapezoid3Side)
                {
                    trapezoids[tn].topTrapezoid0Index = trapezoids[tcurrent].topTrapezoid1Index;
                    trapezoids[tcurrent].topTrapezoid1Index = -1;
                    trapezoids[tn].topTrapezoid1Index = trapezoids[tcurrent].topTrapezoid3Index;

                    trapezoids[trapezoids[tcurrent].topTrapezoid0Index].lowTrapezoid0Index = tcurrent;
                    trapezoids[trapezoids[tn].topTrapezoid0Index].lowTrapezoid0Index = tn;
                    trapezoids[trapezoids[tn].topTrapezoid1Index].lowTrapezoid0Index = tn;
                }
                else
                {
                    trapezoids[tn].topTrapezoid1Index = -1;
                    trapezoids[tn].topTrapezoid0Index = trapezoids[tcurrent].topTrapezoid1Index;
                    trapezoids[tcurrent].topTrapezoid1Index = trapezoids[tcurrent].topTrapezoid0Index;
                    trapezoids[tcurrent].topTrapezoid0Index = trapezoids[tcurrent].topTrapezoid3Index;

                    trapezoids[trapezoids[tcurrent].topTrapezoid0Index].lowTrapezoid0Index = tcurrent;
                    trapezoids[trapezoids[tcurrent].topTrapezoid1Index].lowTrapezoid0Index = tcurrent;
                    trapezoids[trapezoids[tn].topTrapezoid0Index].lowTrapezoid0Index = tn;
                }
                trapezoids[tcurrent].topTrapezoid3Index = trapezoids[tn].topTrapezoid3Index = 0;
            }
            else
            {
                trapezoids[tn].topTrapezoid0Index = trapezoids[tcurrent].topTrapezoid1Index;
                trapezoids[tn].topTrapezoid1Index = trapezoids[tcurrent].topTrapezoid1Index = -1;
                trapezoids[trapezoids[tn].topTrapezoid0Index].lowTrapezoid0Index = tn;
            }
        }
        else
        {
            int tempU = trapezoids[tcurrent].topTrapezoid0Index;
            int tempL0, tempL1;

            if((tempL0 = trapezoids[tempU].lowTrapezoid0Index) > 0 && (tempL1 = trapezoids[tempU].lowTrapezoid1Index) > 0)
            {
                if(trapezoids[tempL0].rightSegmentIndex > 0 && !IsLeftOf(segments[segmentIndex].point1, trapezoids[tempL0].rightSegmentIndex))
                {
                    trapezoids[tcurrent].topTrapezoid0Index = trapezoids[tcurrent].topTrapezoid1Index = trapezoids[tn].topTrapezoid1Index = -1;
                    trapezoids[trapezoids[tn].topTrapezoid0Index].lowTrapezoid1Index = tn;
                }
                else
                {
                    trapezoids[tn].topTrapezoid0Index = trapezoids[tn].topTrapezoid1Index = trapezoids[tcurrent].topTrapezoid1Index = -1;
                    trapezoids[trapezoids[tcurrent].topTrapezoid0Index].lowTrapezoid0Index = tcurrent;
                }
            }
            else
            {
                trapezoids[trapezoids[tcurrent].topTrapezoid0Index].lowTrapezoid0Index = tcurrent;
                trapezoids[trapezoids[tcurrent].topTrapezoid0Index].lowTrapezoid1Index = tn;
            }
        }
    }

    private static void AddSegmentMergeTrapezoids(int segmentIndex, int tfirst, int tlast, bool isLeftSide)
    {
        int tcurrent = tfirst;

        while ((tcurrent > 0) && trapezoids[tcurrent].lowPoint.GreaterThanEqualTo(trapezoids[tlast].lowPoint))
        {
            int tnext;
            bool cond;
            if(isLeftSide)
            {
                cond =  ((tnext = trapezoids[tcurrent].lowTrapezoid0Index) > 0 && segmentIndex == trapezoids[tnext].rightSegmentIndex) ||
                        ((tnext = trapezoids[tcurrent].lowTrapezoid1Index) > 0 && segmentIndex == trapezoids[tnext].rightSegmentIndex);
            }
            else
            {
                cond =  ((tnext = trapezoids[tcurrent].lowTrapezoid0Index) > 0 && segmentIndex == trapezoids[tnext].leftSegmentIndex) ||
                        ((tnext = trapezoids[tcurrent].lowTrapezoid1Index) > 0 && segmentIndex == trapezoids[tnext].leftSegmentIndex);
            }

            if(cond)
            {
                if(trapezoids[tcurrent].leftSegmentIndex == trapezoids[tnext].leftSegmentIndex && trapezoids[tcurrent].rightSegmentIndex == trapezoids[tnext].rightSegmentIndex)
                {
                    int ptnext = nodes[trapezoids[tnext].nodeIndex].parentNodeIndex;
                    if(nodes[ptnext].leftNodeIndex == trapezoids[tnext].nodeIndex)
                    {
                        nodes[ptnext].leftNodeIndex = trapezoids[tcurrent].nodeIndex;
                    }
                    else
                    {
                        nodes[ptnext].rightNodeIndex = trapezoids[tcurrent].nodeIndex;
                    }

                    if((trapezoids[tcurrent].lowTrapezoid0Index = trapezoids[tnext].lowTrapezoid0Index) > 0)
                    {
                        if(tnext == trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid0Index)
                        {
                            trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid0Index = tcurrent;
                        }
                        else if(tnext == trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid1Index)
                        {
                            trapezoids[trapezoids[tcurrent].lowTrapezoid0Index].topTrapezoid1Index = tcurrent;
                        }
                    }

                    if((trapezoids[tcurrent].lowTrapezoid1Index = trapezoids[tnext].lowTrapezoid1Index) > 0)
                    {
                        if(tnext == trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid0Index)
                        {
                            trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid0Index = tcurrent;
                        }
                        else if(tnext == trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid1Index)
                        {
                            trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].topTrapezoid1Index = tcurrent;
                        }
                    }

                    trapezoids[tcurrent].lowPoint = trapezoids[tnext].lowPoint;
                    trapezoids[tnext].trapezoidState = State.INVALID;
                }
                else
                {
                    tcurrent = tnext;
                }
            }
            else
            {
                tcurrent = tnext;
            }
        }
    }

    private static bool IsInserted(int segmentIndex, bool nextPoint)
    {
        if(nextPoint)
        {
            return segments[segments[segmentIndex].nextSegmentIndex].isInserted;
        }
        return segments[segments[segmentIndex].prevSegmentIndex].isInserted;
    }

    private static int FindTrapezoid(Point point0, Point point1, int index)
    {
        while(NodeType.TRAPEZOID != nodes[index].nodeType)
        {
            if(NodeType.VERTEX == nodes[index].nodeType)
            {
                if(point0.GreaterThan(nodes[index].point))
                {
                    index = nodes[index].rightNodeIndex;
                }
                else if(point0.EqualTo(nodes[index].point))
                {
                    if(point1.GreaterThan(nodes[index].point))
                    {
                        index = nodes[index].rightNodeIndex;
                    }
                    else
                    {
                        index = nodes[index].leftNodeIndex;
                    }
                }
                else
                {
                    index = nodes[index].leftNodeIndex;
                }
            }
            else
            {
                if(point0.EqualTo(segments[nodes[index].segmentIndex].point0) || point0.EqualTo(segments[nodes[index].segmentIndex].point1))
                {
                    if(DoubleEqual(point0.y, point1.y))
                    {
                        if(point1.x < point0.x)
                        {
                            index=nodes[index].leftNodeIndex;
                        }
                        else
                        {
                            index=nodes[index].rightNodeIndex;
                        }
                    }
                    else if(IsLeftOf(point1, nodes[index].segmentIndex))
                    {
                        index = nodes[index].leftNodeIndex;
                    }
                    else
                    {
                        index = nodes[index].rightNodeIndex;
                    }
                }
                else if(IsLeftOf(point0, nodes[index].segmentIndex))
                {
                    index = nodes[index].leftNodeIndex;
                }
                else
                {
                    index = nodes[index].rightNodeIndex;
                }
            }
        }
        return nodes[index].trapezoidIndex;
    }

    private static void FindNewRoot(int segmentIndex)
    {
        if(segments[segmentIndex].isInserted)
        {
            return;
        }

        segments[segmentIndex].root0Index = FindTrapezoid(segments[segmentIndex].point0, segments[segmentIndex].point1, segments[segmentIndex].root0Index);
        segments[segmentIndex].root0Index = trapezoids[segments[segmentIndex].root0Index].nodeIndex;

        segments[segmentIndex].root1Index = FindTrapezoid(segments[segmentIndex].point1, segments[segmentIndex].point0, segments[segmentIndex].root1Index);
        segments[segmentIndex].root1Index = trapezoids[segments[segmentIndex].root1Index].nodeIndex;
    }

    private static void InitializeSegmentIndexPermutation(int segmentCount)
    {
        segmentIndexPermutationIndex = 0;
        segmentIndexPermutation = new int[segmentCount];

        for(int i=1; i<=segmentCount; i++)
        {
            segmentIndexPermutation[i-1] = i;
        }
    }
    private static int GetSegmentIndex()
    {
        return segmentIndexPermutation[segmentIndexPermutationIndex++];
    }

    private static int GetNewNodeIndex()
    {
        return nodeAddIndex++;
    }
    private static int GetNewTrapezoidIndex()
    {
        trapezoids[trapezoidAddIndex].leftSegmentIndex = -1;
        trapezoids[trapezoidAddIndex].rightSegmentIndex = -1;
        trapezoids[trapezoidAddIndex].trapezoidState = State.VALID;
        return trapezoidAddIndex++;
    }

    private static bool IsLeftOf(Point point, int segmentIndex)
    {
        double area;

        if(segments[segmentIndex].point1.GreaterThan(segments[segmentIndex].point0))
        {
            if(DoubleEqual(segments[segmentIndex].point0.y, point.y))
            {
                if(point.x < segments[segmentIndex].point0.x)
                {
                    area = 1.0;
                }
                else
                {
                    area = -1.0;
                }
            }
            else if(DoubleEqual(segments[segmentIndex].point1.y, point.y))
            {
                if(point.x < segments[segmentIndex].point1.x)
                {
                    area = 1.0;
                }
                else
                {
                    area = -1.0;
                }
            }
            else
            {
                area = Cross(segments[segmentIndex].point0, segments[segmentIndex].point1, point);
            }
        }
        else
        {
            if(DoubleEqual(segments[segmentIndex].point0.y, point.y))
            {
                if(point.x < segments[segmentIndex].point0.x)
                {
                    area = 1.0;
                }
                else
                {
                    area = -1.0;
                }
            }
            else if(DoubleEqual(segments[segmentIndex].point1.y, point.y))
            {
                if(point.x < segments[segmentIndex].point1.x)
                {
                    area = 1.0;
                }
                else
                {
                    area = -1.0;
                }
            }
            else
            {
                area = Cross(segments[segmentIndex].point1, segments[segmentIndex].point0, point);
            }
        }
        return area > 0.0;
    }
    private static double Cross(Point p0, Point p1, Point p2)
    {
        return (p1.x - p0.x)*(p2.y - p0.y) - (p1.y - p0.y)*(p2.x - p0.x);
    }

    private static int MathLogStar(int n)
    {
        int i;
        double v;
    
        for (i = 0, v = (double) n; v >= 1; i++)
        {
            v = Math.Log(v, 2.0);
        }
        return (i - 1);
    }
    private static int MathN(int n, int h)
    {
        int i;
        double v;
    
        for (i = 0, v = (double) n; i < h; i++)
        {
            v = Math.Log(v, 2f);
        }
        return (int) Math.Ceiling((double)n/v);
    }


    // monotone

    private static bool InsidePolygon(int trapezoidIndex)
    {
        if(State.INVALID == trapezoids[trapezoidIndex].trapezoidState)
        {
            return false;
        }
        if(trapezoids[trapezoidIndex].leftSegmentIndex <= 0 || trapezoids[trapezoidIndex].rightSegmentIndex <= 0)
        {
            return false;
        }

        if((trapezoids[trapezoidIndex].topTrapezoid0Index <= 0 && trapezoids[trapezoidIndex].topTrapezoid1Index <= 0) || (trapezoids[trapezoidIndex].lowTrapezoid0Index <= 0 && trapezoids[trapezoidIndex].lowTrapezoid1Index <= 0))
        {
            int rightSegmentIndex = trapezoids[trapezoidIndex].rightSegmentIndex;
            return segments[rightSegmentIndex].point1.GreaterThan(segments[rightSegmentIndex].point0);
        }
        return false;
    }

    private static void TraversePolygon(int mcurrent, int tcurrent, int from, bool isUp)
    {
        if(tcurrent<=0 || visited[tcurrent])
        {
            return;
        }
        visited[tcurrent] = true;

        if(trapezoids[tcurrent].topTrapezoid0Index <= 0 && trapezoids[tcurrent].topTrapezoid1Index <= 0)
        {
            if(trapezoids[tcurrent].lowTrapezoid0Index > 0 && trapezoids[tcurrent].lowTrapezoid1Index > 0)
            {
                int v0 = trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].leftSegmentIndex;
                int v1 = trapezoids[tcurrent].leftSegmentIndex;
                if(from == trapezoids[tcurrent].lowTrapezoid1Index)
                {
                    int mnew = MakeNewMonotonePolygon(mcurrent, v1, v0);
                    TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
                    TraversePolygon(mnew, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                }
                else
                {
                    int mnew = MakeNewMonotonePolygon(mcurrent, v0, v1);
                    TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                    TraversePolygon(mnew, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
                }
            }
            else
            {
                TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
            }
        }
        else if(trapezoids[tcurrent].lowTrapezoid0Index <= 0 && trapezoids[tcurrent].lowTrapezoid1Index <= 0)
        {
            if(trapezoids[tcurrent].topTrapezoid0Index > 0 && trapezoids[tcurrent].topTrapezoid1Index > 0)
            {
                int v0 = trapezoids[tcurrent].rightSegmentIndex;
                int v1 = trapezoids[trapezoids[tcurrent].topTrapezoid0Index].rightSegmentIndex;
                if(from == trapezoids[tcurrent].topTrapezoid1Index)
                {
                    int mnew = MakeNewMonotonePolygon(mcurrent, v1, v0);
                    TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                    TraversePolygon(mnew, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                }
                else
                {
                    int mnew = MakeNewMonotonePolygon(mcurrent, v0, v1);
                    TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                    TraversePolygon(mnew, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                }
            }
            else
            {
                TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
            }
        }
        else if(trapezoids[tcurrent].topTrapezoid0Index > 0 && trapezoids[tcurrent].topTrapezoid1Index > 0)
        {
            if(trapezoids[tcurrent].lowTrapezoid0Index > 0 && trapezoids[tcurrent].lowTrapezoid1Index > 0)
            {
                int v0 = trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].leftSegmentIndex;
                int v1 = trapezoids[trapezoids[tcurrent].topTrapezoid0Index].rightSegmentIndex;

                if((!isUp && from == trapezoids[tcurrent].lowTrapezoid1Index) || (isUp && from == trapezoids[tcurrent].topTrapezoid1Index))
                {
                    int mnew = MakeNewMonotonePolygon(mcurrent, v1, v0);
                    TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                    TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
                    TraversePolygon(mnew, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                    TraversePolygon(mnew, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                }
                else
                {
                    int mnew = MakeNewMonotonePolygon(mcurrent, v0, v1);
                    TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                    TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                    TraversePolygon(mnew, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                    TraversePolygon(mnew, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
                }
            }
            else
            {
                if(trapezoids[tcurrent].lowPoint.EqualTo(segments[trapezoids[tcurrent].leftSegmentIndex].point1))
                {
                    int v0 = trapezoids[trapezoids[tcurrent].topTrapezoid0Index].rightSegmentIndex;
                    int v1 = segments[trapezoids[tcurrent].leftSegmentIndex].nextSegmentIndex;

                    if(isUp && from == trapezoids[tcurrent].topTrapezoid0Index)
                    {
                        int mnew = MakeNewMonotonePolygon(mcurrent, v1, v0);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                        TraversePolygon(mnew, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                        TraversePolygon(mnew, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                        TraversePolygon(mnew, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
                    }
                    else
                    {
                        int mnew = MakeNewMonotonePolygon(mcurrent, v0, v1);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
                        TraversePolygon(mnew, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                    }
                }
                else
                {
                    int v0 = trapezoids[tcurrent].rightSegmentIndex;
                    int v1 = trapezoids[trapezoids[tcurrent].topTrapezoid0Index].rightSegmentIndex;

                    if(isUp && from == trapezoids[tcurrent].topTrapezoid1Index)
                    {
                        int mnew = MakeNewMonotonePolygon(mcurrent, v1, v0);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                        TraversePolygon(mnew, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
                        TraversePolygon(mnew, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                        TraversePolygon(mnew, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                    }
                    else
                    {
                        int mnew = MakeNewMonotonePolygon(mcurrent, v0, v1);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
                        TraversePolygon(mnew, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                    }
                }
            }
        }
        else if(trapezoids[tcurrent].topTrapezoid0Index > 0 || trapezoids[tcurrent].topTrapezoid1Index > 0)
        {
            if(trapezoids[tcurrent].lowTrapezoid0Index > 0 && trapezoids[tcurrent].lowTrapezoid1Index > 0)
            {
                if(trapezoids[tcurrent].topPoint.EqualTo(segments[trapezoids[tcurrent].leftSegmentIndex].point0))
                {
                    int v0 = trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].leftSegmentIndex;
                    int v1 = trapezoids[tcurrent].leftSegmentIndex;

                    if(!isUp && from == trapezoids[tcurrent].lowTrapezoid0Index)
                    {
                        int mnew = MakeNewMonotonePolygon(mcurrent, v0, v1);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                        TraversePolygon(mnew, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                        TraversePolygon(mnew, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                        TraversePolygon(mnew, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
                    }
                    else
                    {
                        int mnew = MakeNewMonotonePolygon(mcurrent, v1, v0);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                        TraversePolygon(mnew, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                    }
                }
                else
                {
                    int v0 = trapezoids[trapezoids[tcurrent].lowTrapezoid1Index].leftSegmentIndex;
                    int v1 = segments[trapezoids[tcurrent].rightSegmentIndex].nextSegmentIndex;

                    if(!isUp && from == trapezoids[tcurrent].lowTrapezoid1Index)
                    {
                        int mnew = MakeNewMonotonePolygon(mcurrent, v1, v0);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
                        TraversePolygon(mnew, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                        TraversePolygon(mnew, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                        TraversePolygon(mnew, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                        
                    }
                    else
                    {
                        int mnew = MakeNewMonotonePolygon(mcurrent, v0, v1);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                        TraversePolygon(mnew, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
                    }
                }
            }
            else
            {
                if(trapezoids[tcurrent].topPoint.EqualTo(segments[trapezoids[tcurrent].leftSegmentIndex].point0) && trapezoids[tcurrent].lowPoint.EqualTo(segments[trapezoids[tcurrent].rightSegmentIndex].point0))
                {
                    int v0 = trapezoids[tcurrent].rightSegmentIndex;
                    int v1 = trapezoids[tcurrent].leftSegmentIndex;

                    if(isUp)
                    {
                        int mnew = MakeNewMonotonePolygon(mcurrent, v1, v0);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                        TraversePolygon(mnew, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
                        TraversePolygon(mnew, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                        
                    }
                    else
                    {
                        int mnew = MakeNewMonotonePolygon(mcurrent, v0, v1);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                        TraversePolygon(mnew, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                        TraversePolygon(mnew, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                    }
                }
                else if(trapezoids[tcurrent].topPoint.EqualTo(segments[trapezoids[tcurrent].rightSegmentIndex].point1) && trapezoids[tcurrent].lowPoint.EqualTo(segments[trapezoids[tcurrent].leftSegmentIndex].point1))
                {
                    int v0 = segments[trapezoids[tcurrent].rightSegmentIndex].nextSegmentIndex;
                    int v1 = segments[trapezoids[tcurrent].leftSegmentIndex].nextSegmentIndex;

                    if(isUp)
                    {
                        int mnew = MakeNewMonotonePolygon(mcurrent, v1, v0);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                        TraversePolygon(mnew, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
                        TraversePolygon(mnew, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                        
                    }
                    else
                    {
                        int mnew = MakeNewMonotonePolygon(mcurrent, v0, v1);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
                        TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                        TraversePolygon(mnew, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                        TraversePolygon(mnew, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                    }
                }
                else
                {
                    TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid0Index, tcurrent, false);
                    TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid0Index, tcurrent, true);
                    TraversePolygon(mcurrent, trapezoids[tcurrent].topTrapezoid1Index, tcurrent, false);
                    TraversePolygon(mcurrent, trapezoids[tcurrent].lowTrapezoid1Index, tcurrent, true);
                }
            }
        }
    }
    private static int MakeNewMonotonePolygon(int mcurrent, int v0, int v1)
    {
        (int, int) nextVertexPosition = GetVertexPosition(v0, v1);

        int p = vertexChain[v0].vertexPosition[nextVertexPosition.Item1];
        int q = vertexChain[v1].vertexPosition[nextVertexPosition.Item2];

        int i = GetNewMonotoneChainIndex();
        int j = GetNewMonotoneChainIndex();

        monotoneChain[i].vertexNumber = v0;
        monotoneChain[j].vertexNumber = v1;

        monotoneChain[i].next = monotoneChain[p].next;
        monotoneChain[monotoneChain[p].next].prev = i;
        monotoneChain[i].prev = j;
        monotoneChain[j].next = i;
        monotoneChain[j].prev = monotoneChain[q].prev;
        monotoneChain[monotoneChain[q].prev].next = j;

        monotoneChain[p].next = q; // backward?
        monotoneChain[q].prev = p; // backward?

        int f0 = vertexChain[v0].nextfree;
        int f1 = vertexChain[v1].nextfree;

        vertexChain[v0].vertexNext[nextVertexPosition.Item1] = v1;
        vertexChain[v0].vertexPosition[f0] = i;
        vertexChain[v0].vertexNext[f0] = monotoneChain[monotoneChain[i].next].vertexNumber;
        vertexChain[v1].vertexPosition[f1] = j;
        vertexChain[v1].vertexNext[f1] = v0;

        vertexChain[v0].nextfree++;
        vertexChain[v1].nextfree++;

        int mnew = GetNewMonotoneIndex();
        monotoneIndex[mcurrent] = p;
        monotoneIndex[mnew] = i;
        return mnew;

    }
    private static (int, int) GetVertexPosition(int v0, int v1)
    {
        int res0 = 0;
        int res1 = 0;

        double angle = -4.0;
        for(int i=0; i<4; i++)
        {
            if(vertexChain[v0].vertexNext[i] <= 0)
            {
                continue;
            }
            double temp;
            if((temp = GetAngle(vertexChain[v0].point, vertexChain[vertexChain[v0].vertexNext[i]].point, vertexChain[v1].point)) > angle)
            {
                angle = temp;
                res0 = i;
            }
        }

        angle = -4.0;
        for(int i=0; i<4; i++)
        {
            if(vertexChain[v1].vertexNext[i] <= 0)
            {
                continue;
            }
            double temp;
            if((temp = GetAngle(vertexChain[v1].point, vertexChain[vertexChain[v1].vertexNext[i]].point, vertexChain[v0].point)) > angle)
            {
                angle = temp;
                res1 = i;
            }
        }

        return (res0, res1);
    }

    private static double GetAngle(Point p0, Point pn, Point p1)
    {
        Point v0, v1;
        v0.x = pn.x-p0.x;
        v0.y = pn.y-p0.y;

        v1.x = p1.x-p0.x;
        v1.y = p1.y-p0.y;

        if(CrossSine(v0, v1) >= 0)
        {
            return Dot(v0, v1) / Length(v0) / Length(v1);
        }
        else
        {
            return -1.0 * Dot(v0, v1) / Length(v0) / Length(v1) - 2;
        }
    }

    private static double CrossSine(Point p0, Point p1)
    {
        return p0.x * p1.y - p1.x * p0.y;
    }
    private static double Dot(Point p0, Point p1)
    {
        return p0.x * p1.x + p0.y * p1.y;
    }
    private static double Length(Point p0)
    {
        return Math.Sqrt(p0.x * p0.x + p0.y * p0.y);
    }

    private static int GetNewMonotoneIndex()
    {
        return monotoneIndexIndex++;
    }
    private static int GetNewMonotoneChainIndex()
    {
        return monotoneChainIndex++;
    }


    // triangulate single polygon
    private static void TriangulateMonotonePolygon(int vertexCount, int monotonePolygonCount)
    {
        for(int i=0; i<monotonePolygonCount; i++)
        {
            int currentVertexCount = 1;
            bool processed = false;
            int vertex0 = monotoneChain[monotoneIndex[i]].vertexNumber;
            Point pointMax = vertexChain[vertex0].point;
            Point pointMin = vertexChain[vertex0].point;
            int positionMax = monotoneIndex[i];
            int positionMin = monotoneIndex[i];
            int p = monotoneChain[monotoneIndex[i]].next;
            monotoneChain[monotoneIndex[i]].marked = true;
            //Debug.Log(vertex0);
            int v;
            while((v=monotoneChain[p].vertexNumber) != vertex0)
            {
                //Debug.Log(v);
                if(monotoneChain[p].marked)
                {
                    processed = true;
                    break;
                }
                else
                {
                    monotoneChain[p].marked = true;
                }

                if(vertexChain[v].point.GreaterThan(pointMax))
                {
                    //Debug.Log($"{v}, {pointMax.x}, {pointMax.y }" );
                    pointMax = vertexChain[v].point;
                    positionMax = p;
                    //Debug.Log($"{v}, {pointMax.x}, {pointMax.y }" );
                }
                if(vertexChain[v].point.LessThan(pointMin))
                {
                    pointMin = vertexChain[v].point;
                    positionMin = p;
                }

                p = monotoneChain[p].next;
                currentVertexCount++;
                
            }
            if(processed)
            {
                continue;
            }

            if(3 == currentVertexCount)
            {
                res[resCount, 0] = monotoneChain[p].vertexNumber;
                res[resCount, 1] = monotoneChain[monotoneChain[p].next].vertexNumber;
                res[resCount, 2] = monotoneChain[monotoneChain[p].prev].vertexNumber;
                resCount++;
            }
            else
            {
                v = monotoneChain[monotoneChain[positionMax].next].vertexNumber;
                if(vertexChain[v].point.EqualTo(pointMin))
                {
                    TriangulateSinglePolygon(vertexCount, positionMax, false);
                }
                else
                {
                    TriangulateSinglePolygon(vertexCount, positionMax, true);
                }
            }
        }
    }

    private static void TriangulateSinglePolygon(int vertexCount, int positionMax, bool isRight)
    {
        int v;
        int endv, temp, vpos;
        int[] reflexChain = new int[segmentCount+1];
        int reflexChainIndex = 1;

        if(isRight)
        {
            reflexChain[0] = monotoneChain[positionMax].vertexNumber;
            temp = monotoneChain[positionMax].next;
            reflexChain[1] = monotoneChain[temp].vertexNumber;

            vpos = monotoneChain[temp].next;
            v = monotoneChain[vpos].vertexNumber;

            if((endv = monotoneChain[monotoneChain[positionMax].prev].vertexNumber) == 0)
            {
                 endv = vertexCount;
            }
        }
        else
        {
            temp = monotoneChain[positionMax].next;
            reflexChain[0] = monotoneChain[temp].vertexNumber;
            temp = monotoneChain[temp].next;
            reflexChain[1] = monotoneChain[temp].vertexNumber;

            vpos = monotoneChain[temp].next;
            v = monotoneChain[vpos].vertexNumber;

            endv = monotoneChain[positionMax].vertexNumber;
        }

        while((v != endv) || (reflexChainIndex>1))
        {
            if(reflexChainIndex>0)
            {
                if(Cross(vertexChain[v].point, vertexChain[reflexChain[reflexChainIndex-1]].point, vertexChain[reflexChain[reflexChainIndex]].point) > 0.0)
                {
                    res[resCount, 0] = reflexChain[reflexChainIndex-1];
                    res[resCount, 1] = reflexChain[reflexChainIndex];
                    res[resCount, 2] = v;
                    resCount++;
                    reflexChainIndex--;
                }
                else
                {
                    reflexChainIndex++;
                    reflexChain[reflexChainIndex] = v;
                    vpos = monotoneChain[vpos].next;
                    v = monotoneChain[vpos].vertexNumber;
                }
            }
            else
            {
                reflexChainIndex++;
                reflexChain[reflexChainIndex] = v;
                vpos = monotoneChain[vpos].next;
                v = monotoneChain[vpos].vertexNumber;
            }
        }

        res[resCount, 0] = reflexChain[reflexChainIndex-1];
        res[resCount, 1] = reflexChain[reflexChainIndex];
        res[resCount, 2] = v;
        resCount++;
        reflexChainIndex--;
    }
}

}
