﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using HAC.API.Data.Objects;
using HAC.API.Helpers;
using HtmlAgilityPack;

namespace HAC.API.Data
{
    public static class Courses
    {
        public static List<List<AssignmentCourse>> GetAssignmentsFromMarkingPeriod(CookieContainer cookies, Uri requestUri, string link)
        {
            //var assignmentList = new List<AssignmentCourse>();
            var courseList = new List<List<AssignmentCourse>>();
            var documentList = new List<HtmlDocument>();
            string data = Utils.GetData(cookies, requestUri, link, ResponseType.Assignments);

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(data);

            var form = new GradesForm(htmlDocument);
            var reportingPeriodNames = form.ReportingPeriodNames();
            foreach (var name in reportingPeriodNames)
            {
                var body = form.GenerateFormBody(name);
                var response = Utils.GetDataFromReportingPeriod(cookies, requestUri, link, body);
                var doc = new HtmlDocument();
                doc.LoadHtml(response);
                documentList.Add(doc);
            }

            foreach (var document in documentList)
            {
                var localCourseList = new List<AssignmentCourse>();
                var courseHtml = document.DocumentNode.Descendants("div")
                    .Where(node => node.GetAttributeValue("class", "")
                        .Equals("AssignmentClass")).ToList();
                Regex x = new Regex(@"\w+\s-\s\d\s");

                foreach (var courseHtmlItem in courseHtml)
                {
                    var course = courseHtmlItem.Descendants("a")
                        .FirstOrDefault(node => node.GetAttributeValue("class", "")
                            .Equals("sg-header-heading")).InnerText.Trim();
                    var courseName = x.Replace(course, @"").Trim();
                    //removes semester 
                    while (courseName.Substring(courseName.Length - 2) == "S1" ||
                           courseName.Substring(courseName.Length - 2) == "S2")
                    {
                        courseName = courseName.Replace(courseName.Substring(courseName.Length - 2), "");
                        while (courseName.LastOrDefault() == ' ' || courseName.LastOrDefault() == '-')
                        {
                            courseName = courseName.TrimEnd(courseName[^1]);
                        }
                    }

                    var courseId = x.Match(course).ToString().Trim();
                    courseId = courseId.Remove(courseId.Length - 4);
                    //removes excess
                    while (courseId.LastOrDefault() == ' ' || courseId.LastOrDefault() == '-' ||
                           courseId.LastOrDefault() == 'A' || courseId.LastOrDefault() == 'B')
                    {
                        courseId = courseId.TrimEnd(courseId[^1]);
                    }

                    string courseGrade;
                    try
                    {
                        courseGrade = courseHtmlItem.Descendants("span")
                            .FirstOrDefault(node => node.GetAttributeValue("class", "")
                                .Equals("sg-header-heading sg-right"))?.InnerText.Trim().Remove(0, 15)
                            .TrimEnd('%');
                    }
                    catch
                    {
                        continue;
                    }

                    var assignmentList = new List<Assignment>();
                    
                    //gets all the assignments for a course
                    var assignmentTable = courseHtmlItem.Descendants("table").FirstOrDefault(node =>
                        node.GetAttributeValue("class", "").Equals("sg-asp-table"));
                    
                    foreach (var assignmentNode in assignmentTable.Descendants("tr").Where(node =>
                        node.GetAttributeValue("class", "").Equals("sg-asp-table-data-row")))
                    {
                        var assignment = new Assignment();
                        Regex pattern = new Regex(".+:\\s");
                        var assignmentData = assignmentNode.ChildNodes[3].Descendants("a").FirstOrDefault().Attributes["title"].Value;
                        var parsedAssignmentData = Regex.Replace(assignmentData,".+:", "").Trim();
                        
                        
                        foreach (var line in new LineReader(() => new StringReader(parsedAssignmentData)).WithIndex())
                        {
                            switch (line.index)
                            {
                                case 0:
                                    assignment.Title = line.item.Trim();
                                    break;
                                case 1:
                                    assignment.Name = FixAssignmentTitle(line.item.Trim());
                                    break;
                                case 2:
                                    assignment.Category = line.item.Trim();
                                    break;
                                case 3:
                                    var date = DateTime.Parse(line.item.Trim());
                                    assignment.DueDate = date;
                                    break;
                                case 4:
                                    assignment.MaxPoints = double.Parse(line.item.Trim());
                                    break;
                                case 5:
                                    assignment.CanBeDropped = line.item.Contains("Y");
                                    break;
                                case 6:
                                    assignment.ExtraCredit = line.item.Contains("Y");
                                    break;
                                case 7:
                                    assignment.HasAttachments = line.item.Contains("Y");
                                    break;
                            }
                        }
                        
                        var score  = assignmentNode.ChildNodes[5].InnerText.Trim();

                        assignment.Status = score switch
                        {
                            "M" => AssignmentStatus.Missing,
                            "I" => AssignmentStatus.Incomplete,
                            "EXC" => AssignmentStatus.Excused,
                            "L" => AssignmentStatus.Late,
                            _ => AssignmentStatus.Upcoming
                        };
                        
                        if (double.TryParse(score, out var points))
                            assignment.Status = AssignmentStatus.Complete;
                        
                        assignment.Score = score;
                        assignmentList.Add(assignment);
                    }

                    localCourseList.Add(new AssignmentCourse
                    {
                        CourseName = courseName, CourseId = courseId, CourseAverage = double.Parse(courseGrade),
                        Assignments = assignmentList
                    });

                }
                courseList.Add(localCourseList);
            }

            return courseList;
        }

        //Update as needed
        private static string FixAssignmentTitle(string s)
        {
            var symbols = new Dictionary<string,string>
            {
                {"&quot;", "\""},
                {"&amp;", "&"}
            };

            foreach (var symbol in symbols.Keys)
            {
                s = s.Replace(symbol, symbols[symbol]);
            }

            return s;
        }
    }
}