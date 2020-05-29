﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HAC.API.HAC.Objects
{
    public class Response
    {
        public string Message { get; set; }
        public IEnumerable<Course> CurrentAssignmentList { get; set; }
        public IEnumerable<Course> OldAssignmentList { get; set; }
        public IEnumerable<Course> ReportCardList1 { get; set; }
        public IEnumerable<Course> ReportCardList2 { get; set; }
        public IEnumerable<Course> ReportCardList3 { get; set; }
        public IEnumerable<Course> ReportCardList4 { get; set; }
    }
}
