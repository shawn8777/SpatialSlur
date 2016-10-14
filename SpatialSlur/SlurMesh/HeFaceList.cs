﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpatialSlur.SlurCore;

/*
 * Notes
 */

namespace SpatialSlur.SlurMesh
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public partial class HeFaceList:HeElementList<HeFace>
    {
        /// <summary>
        /// 
        /// </summary>
        internal HeFaceList(HeMesh mesh, int capacity = 2)
            : base(mesh, capacity)
        {
        }
     

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vi0"></param>
        /// <param name="vi1"></param>
        /// <param name="vi2"></param>
        /// <returns></returns>
        public HeFace Add(int vi0, int vi1, int vi2)
        {
            return Add(new int[] { vi0, vi1, vi2 });
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="vi0"></param>
        /// <param name="vi1"></param>
        /// <param name="vi2"></param>
        /// <param name="vi3"></param>
        /// <returns></returns>
        public HeFace Add(int vi0, int vi1, int vi2, int vi3)
        {
            return Add(new int[] { vi0, vi1, vi2, vi3 });
        }


        /// <summary>
        /// Adds a new face to the mesh.
        /// </summary>
        /// <param name="vertexIndices"></param>
        /// <returns></returns>
        public HeFace Add(IList<int> vertexIndices)
        {
            int nv = vertexIndices.Count;
            if (nv < 3) return null; // no degenerate faces allowed

            HeVertex[] faceVerts = new HeVertex[nv];
            var verts = Mesh.Vertices;
            int currTag = verts.NextTag;
     
            // collect and validate vertices
            for (int i = 0; i < nv; i++)
            {
                var v = verts[vertexIndices[i]];
           
                // prevents creation of non-manifold faces (those which use the same vertex more than once)
                if (v.Tag == currTag) return null;
                v.Tag = currTag;

                faceVerts[i] = v;
            }

            return AddImpl(faceVerts);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="v0"></param>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <returns></returns>
        public HeFace Add(HeVertex v0, HeVertex v1, HeVertex v2)
        {
            return Add(new HeVertex[] { v0, v1, v2 });
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="v0"></param>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="v3"></param>
        /// <returns></returns>
        public HeFace Add(HeVertex v0, HeVertex v1, HeVertex v2, HeVertex v3)
        {
            return Add(new HeVertex[] { v0, v1, v2, v3});
        }


        /// <summary>
        /// Adds a new face to the mesh.
        /// </summary>
        /// <param name="vertices"></param>
        /// <returns></returns>
        public HeFace Add(IList<HeVertex> vertices)
        {
            if (vertices.Count < 3) return null; // no degenerate faces allowed
            var verts = Mesh.Vertices;
            int currTag = verts.NextTag;

            // validate vertices
            foreach (HeVertex v in vertices)
            {
                verts.OwnsCheck(v);

                // prevents creation of non-manifold faces (those which use the same vertex more than once)
                if (v.Tag == currTag) return null;
                v.Tag = currTag;
            }

            return AddImpl(vertices);
        }
  
       
        /// <summary>
        /// Assumes the given elements are valid for the operation.
        /// http://pointclouds.org/blog/nvcs/martin/index.php
        /// </summary>
        /// <param name="vertices"></param>
        /// <returns></returns>
        internal HeFace AddImpl(IList<HeVertex> vertices)
        {
            int n = vertices.Count;
            Halfedge[] faceLoop = new Halfedge[n];

            // collect all existing halfedges in the new face
            for (int i = 0; i < n; i++)
            {
                HeVertex v = vertices[i];

                // the vertex must be unused or on the mesh boundary
                if (v.IsUnused) continue;
                else if (!v.IsBoundary) return null;

                int j = (i == n - 1) ? 0 : i + 1;
                Halfedge he = v.FindHalfedgeTo(vertices[j]); // search for an existing halfedge between consecutive vertices

                // if a halfedge is found, it can't already have a face
                if (he == null) continue;
                else if (he.Face != null) return null;
                
                faceLoop[i] = he;
            }

            /*
            // avoids creation of non-manifold vertices
            // if two consecutive new halfedges share a used vertex then that vertex will be non-manifold upon adding the face
            for (int i = 0; i < n; i++)
            {
                int j = (i == n - 1) ? 0 : i + 1;
                if (faceLoop[i] == null && faceLoop[j] == null && !vertices[j].IsUnused) 
                    return null;
            }
            */
     
            HeFace newFace = new HeFace();
            Add(newFace); // add face to list
            var hedges = Mesh.Halfedges;

            // create any missing halfedge pairs in the face loop
            for (int i = 0; i < n; i++)
            {
                Halfedge he = faceLoop[i];

                // if missing a halfedge, add a pair between consecutive vertices
                if (he == null)
                {
                    int j = (i == n - 1) ? 0 : i + 1;
                    he = hedges.AddPair(vertices[i], vertices[j]);
                    faceLoop[i] = he;
                }

                he.Face = newFace; // assign the new face
            }

            // link consecutive halfedges
            for (int i = 0; i < n; i++)
            {
                int j = (i == n - 1) ? 0 : i + 1;
                Halfedge he0 = faceLoop[i];
                Halfedge he1 = faceLoop[j];

                Halfedge he2 = he0.Next;
                Halfedge he3 = he1.Previous;
                Halfedge he4 = he0.Twin;

                HeVertex v0 = he0.Start;
                HeVertex v1 = he1.Start;

                // check if halfedges are newly created
                // new halfedges will have null previous or next refs
                int mask = 0;
                if (he2 == null) mask |= 1; // e0 is new
                if (he3 == null) mask |= 2; // e1 is new

                // 0 - neither halfedge is new
                // 1 - he0 is new, he1 is old
                // 2 - he1 is new, he0 is old
                // 3 - both halfedges are new
                switch (mask)
                {
                    case 0:
                        {
                            // neither halfedge is new
                            // if he0 and he1 aren't consecutive, then deal with non-manifold vertex as per http://www.pointclouds.org/blog/nvcs/
                            // otherwise, update the first halfedge from v1 if necessary
                            if (he2 != he1)
                            {
                                Halfedge he = he1.NextBoundaryAtStart(); // find the next boundary halfedge around v1 (must exist if halfedges aren't consecutive)
                                v1.First = he;

                                Halfedge.MakeConsecutive(he.Previous, he2);
                                Halfedge.MakeConsecutive(he3, he);
                                Halfedge.MakeConsecutive(he0, he1);
                            }
                            else if (he1 == v1.First)
                            {
                                Halfedge he = he1.NextBoundaryAtStart(); // find the next boundary halfedge around v1
                                if (he != null) v1.First = he;
                            }
                            break;
                        }
                    case 1:
                        {
                            // he0 is new, he1 is old
                            Halfedge.MakeConsecutive(he3, he4);
                            v1.First = he4;
                            goto default;
                        }
                    case 2:
                        {
                            // he1 is new, he0 is old
                            Halfedge.MakeConsecutive(he1.Twin, he2);
                            goto default;
                        }
                    case 3:
                        {
                            // both halfedges are new
                            // deal with non-manifold case if v1 is already in use
                            if (v1.IsUnused)
                            {
                                Halfedge.MakeConsecutive(he1.Twin, he4);
                            }
                            else
                            {
                                Halfedge.MakeConsecutive(v1.First.Previous, he4); 
                                Halfedge.MakeConsecutive(he1.Twin, v1.First);
                            }
                            v1.First = he4;
                            goto default;
                        }
                    default:
                        {
                            Halfedge.MakeConsecutive(he0, he1); // update refs for inner halfedges
                            break;
                        }
                }
            }

            newFace.First = faceLoop[0]; // set first halfedge in the new face
            return newFace;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="start"></param>
        /// <returns></returns>
        public IEnumerable<HeFace> GetBreadthFirstOrder(HeFace start)
        {
            OwnsCheck(start);

            if (start.IsUnused)
                yield break;

            var queue = new Queue<HeFace>();
            int currTag = NextTag;

            queue.Enqueue(start);
            start.Tag = currTag;

            while (queue.Count > 0)
            {
                HeFace f0 = queue.Dequeue();
                yield return f0;

                foreach (HeFace f1 in f0.AdjacentFaces)
                {
                    if (f1.Tag != currTag)
                    {
                        f1.Tag = currTag;
                        queue.Enqueue(f1);
                    }
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="start"></param>
        /// <returns></returns>
        public IEnumerable<HeFace> GetDepthFirstOrder(HeFace start)
        {
            OwnsCheck(start);

            if (start.IsUnused)
                yield break;

            var stack = new Stack<HeFace>();
            int currTag = NextTag;

            stack.Push(start);
            start.Tag = currTag;

            while (stack.Count > 0)
            {
                HeFace f0 = stack.Pop();
                yield return f0;

                foreach (HeFace f1 in f0.AdjacentFaces)
                {
                    if (f1.Tag != currTag)
                    {
                        f1.Tag = currTag;
                        stack.Push(f1);
                    }
                }
            }
        }


        /// <summary>
        /// Turns all faces to create consistent directionality across the mesh where possible.
        /// This is intended for use on quad meshes.
        /// http://page.math.tu-berlin.de/~bobenko/MinimalCircle/minsurftalk.pdf
        /// </summary>
        public void UnifyFaceOrientation(bool flip)
        {
            Stack<Halfedge> stack = new Stack<Halfedge>();
            int currTag = NextTag;

            for (int i = 0; i < Count; i++)
            {
                HeFace f = this[i];
                if (f.IsUnused || f.Tag == currTag) continue; // skip if unused or already visited

                // check flip
                if (flip) f.First = f.First.Next;
       
                // flag as visited
                f.Tag = currTag;
     
                // add to stack
                stack.Push(f.First);
                UnifyFaceOrientation(stack, currTag);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="start"></param>
        public void UnifyFaceOrientation(HeFace start)
        {
            start.UsedCheck();
            OwnsCheck(start);

            Stack<Halfedge> stack = new Stack<Halfedge>();
            int currTag = NextTag;
        
            // flag as visited
            start.Tag = currTag;

            // add first halfedge to stack
            stack.Push(start.First);
            UnifyFaceOrientation(stack, currTag);
        }


        /// <summary>
        /// 
        /// </summary>
        private void UnifyFaceOrientation(Stack<Halfedge> stack, int currTag)
        {
            var next = new Halfedge[4];

            while (stack.Count > 0)
            {
                Halfedge he0 = stack.Pop();

                foreach(Halfedge he1 in QuadNeighbours(he0))
                {
                    HeFace f1 = he1.Face;
                    if (f1 != null && f1.Tag != currTag)
                    {
                        f1.First = he1;
                        f1.Tag = currTag;
                        stack.Push(he1);
                    }
                }     
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="halfedge"></param>
        /// <returns></returns>
        private IEnumerable<Halfedge> QuadNeighbours(Halfedge halfedge)
        {
            yield return halfedge.Twin.Next.Next; // down
            yield return halfedge.Next.Next.Twin; // up
            yield return halfedge.Previous.Twin.Previous; // left
            yield return halfedge.Next.Twin.Next; // right
        }


        /*
        /// <summary>
        /// 
        /// </summary>
        private void UnifyFaceOrientation(Stack<Halfedge> stack, int currTag)
        {
            while (stack.Count > 0)
            {
                Halfedge he0 = stack.Pop();
       
                // down
                Halfedge he1 = he0.Twin.Next.Next;
                HeFace f1 = he1.Face;
                if(f1 != null && f1.Tag != currTag)
                {
                    f1.First = he1;
                    f1.Tag = currTag;
                    stack.Push(he1);
                }

                // up
                he1 = he0.Next.Next.Twin;
                f1 = he1.Face;
                if (f1 != null && f1.Tag != currTag)
                {
                    f1.First = he1;
                    f1.Tag = currTag;
                    stack.Push(he1);
                }

                // left
                he1 = he0.Previous.Twin.Previous;
                f1 = he1.Face;
                if (f1 != null && f1.Tag != currTag)
                {
                    f1.First = he1;
                    f1.Tag = currTag;
                    stack.Push(he1);
                }

                // right
                he1 = he0.Next.Twin.Next;
                f1 = he1.Face;
                if (f1 != null && f1.Tag != currTag)
                {
                    f1.First = he1;
                    f1.Tag = currTag;
                    stack.Push(he1);
                }
            }
        }
        */


        /// <summary>
        /// 
        /// </summary>
        /// <param name="flip"></param>
        /// <returns></returns>
        public List<List<Halfedge>> GetQuadStrips(bool flip)
        {
            var result = new List<List<Halfedge>>();
            Stack<Halfedge> stack = new Stack<Halfedge>();
            int currTag = NextTag;

            for (int i = 0; i < Count; i++)
            {
                HeFace f = this[i];
                if (f.IsUnused || f.Tag == currTag) continue; // skip if unused or already visited

                stack.Push((flip)? f.First.Next: f.First);
                GetQuadStrips(stack, currTag, result);
            }

            return result;
        }

       
        /// <summary>
        /// 
        /// </summary>
        /// <param name="start"></param>
        /// <param name="flip"></param>
        /// <returns></returns>
        public List<List<Halfedge>> GetQuadStrips(HeFace start, bool flip)
        {
            start.UsedCheck();
            OwnsCheck(start);

            var result = new List<List<Halfedge>>();
            Stack<Halfedge> stack = new Stack<Halfedge>();
     
            stack.Push((flip) ? start.First.Next : start.First);
            GetQuadStrips(stack, NextTag, result);

            return result;
        }


        /// <summary>
        /// 
        /// </summary>
        private void GetQuadStrips(Stack<Halfedge> stack, int currTag, List<List<Halfedge>> result)
        {
            while (stack.Count > 0)
            {
                Halfedge he0 = stack.Pop();
                HeFace f0 = he0.Face;

                // don't start from boundary halfedges or those with visited faces
                if (f0 == null || f0.Tag == currTag) continue; 

                // backtrack to first encountered visited face or boundary
                Halfedge he1 = he0;
                HeFace f1 = he1.Face;
                do
                {
                    he1 = he1.Twin.Next.Next;
                    f1 = he1.Face;
                } while (f1 != null && f1.Tag != currTag && f1 != f0);

                // collect halfedges in strip
                List<Halfedge> strip = new List<Halfedge>();
                he1 = he1.Previous.Previous.Twin;
                f1 = he1.Face;
                do
                {
                    // add left and right neighbours to stack
                    // TODO only add if not yet visited and @ irregular vertex or boundary
                    stack.Push(he1.Previous.Twin.Previous);
                    stack.Push(he1.Next.Twin.Next);

                    // add current halfedge to strip and flag face as visited
                    strip.Add(he1);
                    f1.Tag = currTag;

                    // advance to next halfedge
                    he1 = he1.Next.Next.Twin;
                    f1 = he1.Face;
                } while (f1 != null && f1.Tag != currTag);

                strip.Add(he1); // add last halfedge
                result.Add(strip);
            }
        }


        /*
        /// <summary>
        /// 
        /// </summary>
        private void GetFaceStrips(Stack<Halfedge> stack, int currTag, List<List<Halfedge>> result)
        {
            while (stack.Count > 0)
            {
                Halfedge first = stack.Pop();
                HeFace f = first.Face;
                if (f == null || f.Tag == currTag) continue; // don't start from boundary halfedges or those with visited faces

                // backtrack to first encountered visited face or boundary
                Halfedge he = first;
                f = he.Twin.Face;
                while (f != null && f.Tag != currTag)
                {
                    he = he.Twin.Next.Next; // down
                    f = he.Twin.Face;
                    if (he == first) break; // break if back at first halfedge
                }

                // collect halfedges in strip
                List<Halfedge> strip = new List<Halfedge>();
                f = he.Face;
                do
                {
                    // TODO perform visited check & null check before adding to the stack
                    // flag elements as soon as they're added to stack to avoid dups

                    // add left/right neighbours to stack
                    stack.Push(he.Previous.Twin.Previous);
                    stack.Push(he.Next.Twin.Next);

                    // add current halfedge to strip and flag face as visited
                    strip.Add(he);
                    f.Tag = currTag;

                    // advance to next halfedge
                    he = he.Next.Next.Twin;
                    f = he.Face;
                } while (f != null && f.Tag != currTag);

                strip.Add(he); // add last halfedge
                result.Add(strip);
            }
        }
        */


        /// <summary>
        /// Orients each face such that the first halfedge returns the minimum value for the given function.
        /// </summary>
        /// <param name="selector"></param>
        /// <param name="parallel"></param>
        public void OrientFacesToMin(Func<Halfedge,double> selector, bool parallel = false)
        {
            if (parallel)
                Parallel.ForEach(Partitioner.Create(0, Count), range =>
                    OrientFacesToMin(selector, range.Item1, range.Item2));
            else
                OrientFacesToMin(selector, 0, Count);
        }


        /// <summary>
        /// 
        /// </summary>
        private void OrientFacesToMin(Func<Halfedge, double> selector, int i0, int i1)
        {
            for (int i = i0; i < i1; i++)
            {
                HeFace f = this[i];
                if (f.IsUnused) continue;

                f.First = f.Halfedges.SelectMin(selector);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="parallel"></param>
        public void OrientFacesToBoundary(bool parallel)
        {
            if (parallel)
                Parallel.ForEach(Partitioner.Create(0, Count), range =>
                    OrientFacesToBoundary(range.Item1, range.Item2));
            else
                OrientFacesToBoundary(0, Count);
        }


        /// <summary>
        /// 
        /// </summary>
        private void OrientFacesToBoundary(int i0, int i1)
        {
            for (int i = i0; i < i1; i++)
            {
                HeFace f = this[i];
                if (f.IsUnused) continue;

                Halfedge he = f.FindBoundary();
                if (he != null) f.First = he;
            }
        }


        /// <summary>
        /// Orients each quad such that the first halfedge has the shortest diagonal.
        /// This is intended for use on quad meshes.
        /// </summary>
        /// <param name="parallel"></param>
        public void OrientQuadsToMinDiagonal(bool parallel = false)
        {
            if (parallel)
                Parallel.ForEach(Partitioner.Create(0, Count), range =>
                    OrientQuadsToMinDiagonal(range.Item1, range.Item2));
            else
                OrientQuadsToMinDiagonal(0, Count);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="i0"></param>
        /// <param name="i1"></param>
        private void OrientQuadsToMinDiagonal(int i0, int i1)
        {
            for (int i = i0; i < i1; i++)
            {
                HeFace f = this[i];
                if (f.IsUnused) continue;

                Halfedge he0 = f.First;
                Halfedge he1 = he0.Next.Next;
                if (he0.Previous != he1.Next) continue; // quad check

                // compare diagonals
                Vec3d p0 = he0.Start.Position;
                Vec3d p1 = he0.End.Position;
                Vec3d p2 = he1.Start.Position;
                Vec3d p3 = he1.End.Position;

                if (p0.SquareDistanceTo(p2) > p1.SquareDistanceTo(p3))
                    f.First = he0.Next;
            }
        }


        /// <summary>
        /// Counts the number of faces adjacent to both given faces.
        /// </summary>
        /// <param name="f0"></param>
        /// <param name="f1"></param>
        /// <returns></returns>
        public int CountCommonNeighbours(HeFace f0, HeFace f1)
        {
            OwnsCheck(f0);
            OwnsCheck(f1);

            if (f0.IsUnused || f1.IsUnused)
                return 0;

            return CountCommonNeighboursImpl(f0, f1);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="f0"></param>
        /// <param name="f1"></param>
        /// <returns></returns>
        internal int CountCommonNeighboursImpl(HeFace f0, HeFace f1)
        {
            int currTag = NextTag;

            // flag neighbours of f1
            foreach (HeFace f in f1.AdjacentFaces)
                f.Tag = currTag;

            // count flagged neighbours of f0
            int count = 0;
            foreach (HeFace f in f0.AdjacentFaces)
                if (f.Tag == currTag) count++;

            return count;
        }


        /// <summary>
        /// Returns all faces adjacent to both given faces.
        /// </summary>
        /// <param name="f0"></param>
        /// <param name="f1"></param>
        /// <returns></returns>
        public List<HeFace> GetCommonNeighbours(HeFace f0, HeFace f1)
        {
            OwnsCheck(f0);
            OwnsCheck(f1);

            if (f0.IsUnused || f1.IsUnused)
                return new List<HeFace>();

            return GetCommonNeighboursImpl(f0, f1);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="f0"></param>
        /// <param name="f1"></param>
        /// <returns></returns>
        internal List<HeFace> GetCommonNeighboursImpl(HeFace f0, HeFace f1)
        {
            int currTag = NextTag;

            // flag neighbours of f1
            foreach (HeFace f in f1.AdjacentFaces)
                f.Tag = currTag;

            // count flagged neighbours of f0
            List<HeFace> result = new List<HeFace>();
            foreach (HeFace f in f0.AdjacentFaces)
                if (f.Tag == currTag) result.Add(f);

            return result;
        }


        /// <summary>
        /// Counts the number of vertices shared by the two given faces.
        /// </summary>
        /// <param name="f0"></param>
        /// <param name="f1"></param>
        /// <returns></returns>
        public int CountCommonVertices(HeFace f0, HeFace f1)
        {
            OwnsCheck(f0);
            OwnsCheck(f1);

            if (f0.IsUnused || f1.IsUnused)
                return 0;

            return CountCommonVerticesImpl(f0, f1);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="f0"></param>
        /// <param name="f1"></param>
        /// <returns></returns>
        internal int CountCommonVerticesImpl(HeFace f0, HeFace f1)
        {
            int currTag = Mesh.Vertices.NextTag;

            // flag neighbours of f1
            foreach (HeVertex v in f1.Vertices)
                v.Tag = currTag;

            // count flagged neighbours of f0
            int count = 0;
            foreach (HeVertex v in f0.Vertices)
                if (v.Tag == currTag) count++;

            return count;
        }


        /// <summary>
        /// Returns all vertices shared by both given faces.
        /// </summary>
        /// <param name="f0"></param>
        /// <param name="f1"></param>
        /// <returns></returns>
        public List<HeVertex> GetCommonVertices(HeFace f0, HeFace f1)
        {
            OwnsCheck(f0);
            OwnsCheck(f1);

            if (f0.IsUnused || f1.IsUnused)
                return new List<HeVertex>();

            return GetCommonVerticesImpl(f0, f1);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="f0"></param>
        /// <param name="f1"></param>
        /// <returns></returns>
        internal List<HeVertex> GetCommonVerticesImpl(HeFace f0, HeFace f1)
        {
            int currTag = Mesh.Vertices.NextTag;

            // flag neighbours of f1
            foreach (HeVertex v in f1.Vertices)
                v.Tag = currTag;

            // count flagged neighbours of f0
            List<HeVertex> result = new List<HeVertex>();
            foreach (HeVertex v in f0.Vertices)
                if (v.Tag == currTag) result.Add(v);

            return result;
        }


        #region Euler Operators

        /// <summary>
        /// Removes a face from the mesh as well as any invalid elements created in the process.
        /// Returns true on success.
        /// </summary>
        /// <param name="face"></param>
        public void Remove(HeFace face)
        {
            OwnsCheck(face);
            face.UsedCheck();
            RemoveImpl(face);
        }


        /// <summary>
        /// Assumes the given elements are valid for the operation.
        /// </summary>
        /// <param name="face"></param>
        /// <returns></returns>
        internal void RemoveImpl(HeFace face)
        {
            /*
            // avoids creation of non-manifold vertices
            foreach (HeVertex v in face.Vertices)
                if (v.IsBoundary && v.First.Twin.Face != face) return false;
            */
        
            Halfedge he = face.First;
            HeVertex v0 = he.Start;
            HeVertex v1;

            // update halfedge->face refs
            // can't just circulate face since halfedge connectivity is changed within loop
            var hedges = Mesh.Halfedges;
            do
            {
                v1 = he.End; // cache end vertex before modifying topology

                if (he.Twin.Face == null)
                {
                    hedges.RemovePair(he);
                }
                else
                {
                    he.MakeFirstAtStart();
                    he.Face = null;
                }

                he = he.Next;
            } while (v1 != v0);

            // flag for removal
            face.MakeUnused();
        }


        /// <summary>
        /// Removes a halfedge pair, merging their two adajcent faces.
        /// The face adjacent to the given halfedge is removed.
        /// </summary>
        /// <param name="halfedge"></param>
        /// <returns></returns>
        public void MergeFaces(Halfedge halfedge)
        {
            Mesh.Halfedges.OwnsCheck(halfedge);
            halfedge.UsedCheck();
            MergeFacesImpl(halfedge);
        }


        /// <summary>
        /// Assumes the given elements are valid for the operation.
        /// </summary>
        /// <param name="halfedge"></param>
        /// <returns></returns>
        internal void MergeFacesImpl(Halfedge halfedge)
        {
            Halfedge he0 = halfedge;
            Halfedge he1 = he0.Twin;

            HeFace f0 = he0.Face; // to be removed
            HeFace f1 = he1.Face;

            // if halfedge is on boundary, just remove the existing face
            if (f0 == null)
            {
                RemoveImpl(f1);
                return;
            }
            else if (f1 == null)
            {
                RemoveImpl(f0);
                return;
            }

            // update face refs for all halfedges in f0
            foreach(Halfedge he in he0.CirculateFace.Skip(1))
                he.Face = f1;

            // remove halfedge pair between the two faces
            var hedges = Mesh.Halfedges;
            hedges.RemovePair(he0);

            // clean up potential valence 1 vertices
            he0 = he0.Next;
            while (he0.IsAtDegree1)
            {
                hedges.RemovePair(he0);
                he0 = he0.Next;
            }

            he1 = he1.Next;
            while (he1.IsAtDegree1)
            {
                hedges.RemovePair(he1);
                he1 = he1.Next;
            }

            // update face->halfedge ref if necessary
            if (f1.First.IsUnused) 
                f1.First = he1;

            // flag elements for removal
            f0.MakeUnused();
        }
   

        /// <summary>
        /// Splits a face by creating a new halfedge pair between the start vertices of the given halfedges.
        /// Returns the new halfedge adjacent to the new face on success or null on failure.
        /// </summary>
        /// <param name="he0"></param>
        /// <param name="he1"></param>
        /// <returns></returns>
        public Halfedge SplitFace(Halfedge he0, Halfedge he1)
        {
            var hedges = Mesh.Halfedges;
            hedges.OwnsCheck(he0);
            hedges.OwnsCheck(he1);
         
            he0.UsedCheck();
            he1.UsedCheck();

            // halfedges must be on the same face which can't be null
            if (he0.Face == null || he0.Face != he1.Face) 
                return null;

            // halfedges can't be consecutive
            if (Halfedge.AreConsecutive(he0, he1)) 
                return null;

            return SplitFaceImpl(he0, he1);
        }


        /// <summary>
        /// Assumes the given elements are valid for the operation.
        /// </summary>
        /// <param name="he0"></param>
        /// <param name="he1"></param>
        /// <returns></returns>
        internal Halfedge SplitFaceImpl(Halfedge he0, Halfedge he1)
        {
            HeFace f0 = he0.Face;
            HeFace f1 = new HeFace();
            Add(f1);

            Halfedge he2 = Mesh.Halfedges.AddPair(he0.Start, he1.Start);
            Halfedge he3 = he2.Twin;

            // set halfedge->face refs
            he3.Face = f0;
            he2.Face = f1;

            // set new halfedges as first in respective faces
            f0.First = he3;
            f1.First = he2;

            // update halfedge->halfedge refs
            Halfedge.MakeConsecutive(he0.Previous, he2);
            Halfedge.MakeConsecutive(he1.Previous, he3);
            Halfedge.MakeConsecutive(he3, he0);
            Halfedge.MakeConsecutive(he2, he1);

            // update face references of all halfedges in new loop
            Halfedge he = he2.Next;
            do
            {
                he.Face = f1;
                he = he.Next;
            } while (he != he2);

            return he2; // return halfedge adjacent to new face
        }


        /// <summary>
        /// Returns the new vertex created at the center of the given face.
        /// </summary>
        /// <param name="face"></param>
        public HeVertex Stellate(HeFace face)
        {
            OwnsCheck(face);
            face.UsedCheck();
            return StellateImpl(face, face.GetBarycenter());
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="face"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public HeVertex Stellate(HeFace face, Vec3d point)
        {
            OwnsCheck(face);
            face.UsedCheck();
            return StellateImpl(face, point);
        }


        /// <summary>
        /// Assumes the given elements are valid for the operation.
        /// </summary>
        internal HeVertex StellateImpl(HeFace face, Vec3d point)
        {
            // add new vertex at face center
            HeVertex fc = Mesh.Vertices.Add(point);

            // hold first halfedge and vertex in the face
            Halfedge he = face.First;
            HeVertex v = he.Start;

            // create new halfedges and connect to existing ones
            var hedges = Mesh.Halfedges;
            do
            {
                Halfedge he0 = hedges.AddPair(he.Start, fc);
                Halfedge.MakeConsecutive(he.Previous, he0);
                Halfedge.MakeConsecutive(he0.Twin, he);
                he = he.Next;
            } while (he.Start != v);

            he = face.First; // reset to first halfedge in face
            fc.First = he.Previous; // set outgoing halfedge for the central vertex

            // connect new halfedges and create new faces where necessary
            do
            {
                Halfedge he0 = he.Previous;
                Halfedge he1 = he.Next;
                Halfedge.MakeConsecutive(he1, he0);

                // create new face if necessary
                if (face == null)
                {
                    face = new HeFace();
                    Add(face);
                    face.First = he;
                    he.Face = face;
                }

                // assign halfedge->face refs
                he0.Face = face;
                he1.Face = face;
                face = null;

                he = he1.Twin.Next;
            } while (he.Start != v);

            return fc;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="halfedge"></param>
        /// <returns></returns>
        public HeFace FillHole(Halfedge halfedge)
        {
            Mesh.Halfedges.OwnsCheck(halfedge);
            halfedge.UsedCheck();

            // halfedge can't already have a face
            if (halfedge.Face != null)
                return null;

            return FillHoleImpl(halfedge);
        }


        /// <summary>
        /// Assumes the given elements are valid for the operation.
        /// </summary>
        /// <param name="halfedge"></param>
        /// <returns></returns>
        internal HeFace FillHoleImpl(Halfedge halfedge)
        {
            HeFace f = new HeFace();
            Add(f);
            f.First = halfedge;

            // assign to halfedges
            foreach (Halfedge he in halfedge.CirculateFace)
                he.Face = f;

            return f;
        }


        /// <summary>
        /// Triangulates a given face without adding any vertices.
        /// </summary>
        /// <param name="face"></param>
        /// <returns></returns>
        public Halfedge Triangulate(HeFace face)
        {
            // TODO
            throw new NotImplementedException();
        }

        #endregion

    }
}
