﻿using System;
using System.Collections;
using System.Collections.Generic;

using static SpatialSlur.SlurCore.CoreUtil;

/*
* Notes
*/

namespace SpatialSlur.SlurData
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public struct ReadOnlyListView<T> : IReadOnlyList<T>
    {
        private readonly List<T> _source;
        private readonly int _start;
        private readonly int _count;


        /// <summary>
        /// 
        /// </summary>
        public int Start
        {
            get { return _start; }
        }


        /// <summary>
        /// 
        /// </summary>
        public int Count
        {
            get { return _count; }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public T this[int index]
        {
            get
            {
                BoundsCheck(index, _count);
                return _source[index + _start];
            }
        }


        /// <summary>
        /// Returns true if the view is still valid.
        /// Changes to the underlying list might invalidate a view.
        /// </summary>
        public bool IsValid
        {
            get { return _start + _count <= _source.Count; }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="start"></param>
        /// <param name="count"></param>
        public ReadOnlyListView(List<T> source, int start, int count)
        {
            if (start < 0 || count < 0 || start + count > source.Count)
                throw new ArgumentOutOfRangeException();

            _source = source;
            _start = start;
            _count = count;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="start"></param>
        /// <param name="count"></param>
        public ReadOnlyListView(ReadOnlyListView<T> other, int start, int count)
            : this(other._source, other._start + start, count)
        {
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ListView<T>.Enumerator GetEnumerator()
        {
            return new ListView<T>.Enumerator(_source, _start, _count);
        }


        #region Explicit interface implementations

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return GetEnumerator();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
