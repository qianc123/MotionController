﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using HalconDotNet;

namespace HalconCCD
{
    class GlobalVar
    {
        /// <summary>
        /// 判断是否在CCD流程中
        /// </summary>
        public static bool CCD_Worked = false;
        /// <summary>
        /// 判断程序是否在运行中
        /// </summary>
        public static bool App_Run = false;
        /// <summary>
        /// 解析结果
        /// </summary>
        public static Dictionary<int, int> CCD_Result = new Dictionary<int, int>();
        /// <summary>
        /// 解析缩略图
        /// </summary>
        public static Dictionary<int, Bitmap> CCD_Image = new Dictionary<int, Bitmap>();
        /// <summary>
        /// 测试图片存储路径
        /// </summary>
        public static HTuple TestData = "D:/TESTDATA/";
        /// <summary>
        /// 连接方式
        /// </summary>
        public static string ConnectMode = "GigE";
    }
}
