﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using HalconDotNet;
using static HalconCCD.EnumValue;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.IO;
using ViewROI;
using System.Diagnostics;

namespace HalconCCD
{

    public partial class CCDShow : UserControl
    {
        #region 属性窗口
        private string m_CameraName = string.Empty;
        /// <summary>
        /// 相机的位置【名称】
        /// </summary>
        [Category("自定义"), Browsable(true), Description("相机的名称")]
        public string CameraDefine
        {
            get { return m_CameraName; }
            set
            {
                this.m_CameraName = value;
                //this.label_Title.Text = this.m_CameraName;
            }
        }
        private string m_ConnectMode = string.Empty;
        /// <summary>
        /// 相机连接方式
        /// </summary>
        public string ConnectMode
        {
            set
            {
                this.m_ConnectMode = value;
            }
            get { return m_ConnectMode; }
        }
        #endregion


        /// <summary>
        /// 是否实时播放
        /// </summary>
        private ManualResetEventSlim Player = new ManualResetEventSlim(false);
        /// <summary>
        /// 是否开启实时播放
        /// </summary>
        internal bool Run { get { return Player.IsSet; } }

        /// <summary>
        /// 实时播放时 执行匹配的线程
        /// </summary>
        private Thread thd;

        /// <summary>
        /// 相机名称【连接相机使用】
        /// </summary>
        internal string CameraName
        {
            get { return this.m_CameraName.ToString(); }
        }

        /// <summary>
        /// 相机 最后一张图片
        /// </summary>
        public HImage LastImage
        {
            get { return Grab_Image == null ? null : RotateImage(new HImage(Grab_Image)); }
        }
        //旋转图片
        private HImage RotateImage(HImage image)
        {
            return image.RotateImage(90.0d, "constant");
        }
        /// <summary>
        /// 按照指定角度旋转图片
        /// </summary>
        /// <param name="image"></param>
        /// <param name="Phi"></param>
        /// <returns></returns>
        private HImage RotateImageWithPhi(HImage image, double Phi)
        {
            return image.RotateImage(Phi, "constant");
        }
        private CCDStatus m_Status = CCDStatus.Unknown;
        /// <summary>
        /// 相机状态
        /// </summary>
        public CCDStatus Status
        {
            get { return m_Status; }
            set
            {
                if (m_Status != value) Label_Online(value, value == CCDStatus.Online ? Color.LimeGreen : Color.Red, value == CCDStatus.Online ? false : true);
                m_Status = value;
            }
        }

        /// <summary>
        /// 显示相机状态【在线、离线、异常】
        /// </summary>
        private void Label_Online(CCDStatus status, Color forecolor, bool IsErr)
        {
            if (this.label_CCDStatus.InvokeRequired)
            {
                this.label_CCDStatus.BeginInvoke(new Action(delegate { Label_Online(status, forecolor, IsErr); }));
            }
            else
            {
                string text = string.Empty;
                switch (status)
                {
                    case CCDStatus.Unknown:
                        text = "未知";
                        break;
                    case CCDStatus.Online:
                        text = "在线";
                        break;
                    case CCDStatus.Offline:
                        text = "离线";
                        break;
                    case CCDStatus.OpenError:
                        text = "开启异常";
                        break;
                    case CCDStatus.CloseError:
                        text = "关闭异常";
                        break;
                    case CCDStatus.GrabError:
                        text = "抓取异常";
                        break;
                    case CCDStatus.OtherError:
                        text = "其他异常";
                        break;
                }
                this.label_CCDStatus.Text = text;
                this.label_CCDStatus.ForeColor = forecolor;

                Event_ShowStr(string.Format("{0} {1}", CameraName, text), IsErr);
            }
        }

        /// <summary>
        /// 事件-显示字符串
        /// </summary>
        /// <param name="str"></param>
        private void Event_ShowStr(string str, bool IsErr = false)
        {
            if (this.event_ShowStr != null) this.event_ShowStr(str, IsErr);
        }

        // Local iconic variables 
        HObject Grab_Image = null;
        // Local control variables 
        HTuple hv_AcqHandle = null;
        /// <summary>
        /// 大于4次则回收垃圾
        /// </summary>
        private static int GCCount = 0;
        /// <summary>
        /// 相机是否开启
        /// </summary>
        private bool FramegrabberIsOpen = false;
        /// <summary>
        /// 区块集合
        /// </summary>
        private BitMapBlock[] block_List = new BitMapBlock[16];


        private Logs log = Logs.LogsT();


        /// <summary>
        /// 抓取图像 的 锁
        /// </summary>
        private object lockshow = new object();

        /// <summary>
        /// 多次抓取，等待抓取完成
        /// </summary>
        internal AutoResetEvent PhotoMark = new AutoResetEvent(false);

        public delegate void dele_ShowStr(string str, bool IsErr);//委托-显示字符串
        public event dele_ShowStr event_ShowStr;

        public CCDShow()
        {
            InitializeComponent();

            InitView();
            InitBlock();
        }

        private void InitBlock()
        {
        }

        /// <summary>
        /// 抓取一次
        /// </summary>
        public void PlayerOnce()
        {
            GrabImage(0);
        }

        /// <summary>
        /// 暂停播放
        /// </summary>
        public void PlayerPause()
        {
            Player.Reset();
        }

        public HWndCtrl mView;
        private HXLD DetectionContour;
        private void InitView()
        {
            mView = new HWndCtrl(hWindowControl_Player, false);

        }

        ToolTip tooltip = new ToolTip();
        MyFunction myf = new MyFunction();
        private void CCDShow_Load(object sender, EventArgs e)
        {
            InitalControls();
            GlobalVar.App_Run = true;
            Thread thd_GradTime = new Thread(GradTime_Check);
            thd_GradTime.IsBackground = true;
            // thd_GradTime.Start();
        }


        private void InitalControls()
        {
            this.BeginInvoke(new Action(() =>
            {
                this.label_CCDStatus.Text = Status.ToString();
                this.lb_UpBarcode.Text = "条码一：";
                this.lb_DownBarcode.Text = "条码二：";
            }));
        }

        private void btn_Start_Click(object sender, EventArgs e)
        {
            PlayerRun();
            //GrabImage(0, true);
        }
        /// <summary>
        /// 开启播放
        /// </summary>
        public void PlayerRun()
        {
            if (thd == null || (thd != null && !thd.IsAlive))
            {
                thd = new Thread(Thd_GrabImage);
                thd.IsBackground = true;
                thd.Name = this.m_CameraName.ToString() + "线程";
                thd.Start();
            }
            Player.Set();

        }
        int GrabMode = 0;
        // Main procedure 
        private void Thd_GrabImage()
        {
            // Default settings used in HDevelop 
            HOperatorSet.SetSystem("width", 512);
            HOperatorSet.SetSystem("height", 512);

            //Image Acquisition 01: Code generated by Image Acquisition 01
            try
            {
                while (Player.Wait(-1))
                {
                    GrabImage(GrabMode, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:fff"));
                log.AddERRORLOG("相机采集异常:" + ex.Message);
            }
            finally
            {
                CloseCamera();
            }
        }
        /// <summary>
        /// 监控拍照时长
        /// </summary>
        private void GradTime_Check()
        {
            while (GlobalVar.App_Run)
            {
                try
                {
                    if (grab_time.Elapsed.TotalSeconds > 5)
                    {
                        EndWork();
                        CloseCamera();
                        grab_time.Reset();
                        log.AddERRORLOG("相机拍照超时");
                    }

                }
                catch (Exception ex)
                {
                    log.AddERRORLOG("相机拍照监控线程异常:" + ex.Message + ex.StackTrace);
                }
            }
        }
        public bool IsGrabImage { get { return m_InGrabImage; } }
        private bool m_InGrabImage = false;//是否处于抓取中       
        DateTime lastgrab;
        /// <summary>
        /// 抓取图像
        /// </summary>
        /// <param name="ActionIndex">0：只抓图；1：二维码；</param>
        /// <param name="Async">是否异步</param>
        private void GrabImage(int ActionIndex, bool Async = false)
        {
            try
            {
                m_InGrabImage = true;
                lock (lockshow)
                {
                    lastgrab = DateTime.Now;
                    if (!FramegrabberIsOpen && !OpenCamera()) return;

                    Grab_Image.Dispose();

                    HObject m_ROI_image = null;
                    if (m_ROI_image != null) m_ROI_image.Dispose();

                    if (Async) HOperatorSet.GrabImageAsync(out Grab_Image, hv_AcqHandle, -1);
                    else HOperatorSet.GrabImage(out Grab_Image, hv_AcqHandle);

                    //Image Acquisition 01: Do something
                    Grab_Image = RotateImageWithPhi(new HImage(Grab_Image), 90);
                    // Grab_Image = RotateImage(new HImage(Grab_Image));//先 旋转图片
                    m_ROI_image = Grab_Image;

                    switch (ActionIndex)
                    {
                        case 0:
                            DirectShowImage(m_ROI_image);
                            break;
                        case 1:
                            DirectShowImage(m_ROI_image);
                            HandleBarcode(m_ROI_image);
                            break;

                    }
                    Console.WriteLine("{0}\tTest Time:{1}", this.m_CameraName, DateTime.Now.ToString("HH:mm:ss:fff"));
                    if (FramegrabberIsOpen) Status = CCDStatus.Online;//相机开启时，改变状态，否则不修改
                }
            }
            catch (Exception ex)
            {
                Status = CCDStatus.GrabError;
                if (ex.Message.Contains("device lost"))
                {
                    CloseCamera();
                }

                log.AddERRORLOG("抓取图像异常:" + ex.Message);
            }
            finally
            {
                if (GCCount++ > 20)
                {
                    GC.Collect();
                    GCCount = 0;
                }
                Thread.Sleep(10);
                m_InGrabImage = false;
            }
        }

        Stopwatch grab_time = new Stopwatch();
        /// <summary>
        /// 拍照【作业流程】
        /// </summary>
        /// <param name="Pcs_Index">制品pcs序号 </param>
        /// <param name="folder">保存地址</param>
        /// <param name="needRotate">是否需要旋转</param>
        /// <param name="Async">是否异步</param>
        public string GrabImage_Working(int Pcs_Index, string folder, bool needRotate, bool Async = false)
        {
            PlayerPause();//暂停实时
            string barcode = null;
            grab_time.Start();
            try
            {
                m_InGrabImage = true;
                lock (lockshow)
                {
                    lastgrab = DateTime.Now;
                    if (!FramegrabberIsOpen && !OpenCamera()) return null;

                    Grab_Image.Dispose();

                    HObject m_ROI_image = null;
                    if (m_ROI_image != null) m_ROI_image.Dispose();

                    if (Async) HOperatorSet.GrabImageAsync(out Grab_Image, hv_AcqHandle, -1);
                    else HOperatorSet.GrabImage(out Grab_Image, hv_AcqHandle);

                    //如果需要旋转图片
                    Grab_Image = RotateImage(new HImage(Grab_Image));//先 旋转图片
                    m_ROI_image = Grab_Image;

                    DirectShowImage(m_ROI_image);

                    HandleBarcode(m_ROI_image, Pcs_Index, folder, out barcode);
                    grab_time.Restart();
                }
                Console.WriteLine("{0}\tTest Time:{1}", this.m_CameraName, DateTime.Now.ToString("HH:mm:ss:fff"));
                if (FramegrabberIsOpen) Status = CCDStatus.Online;//相机开启时，改变状态，否则不修改
                return barcode;
            }
            catch (Exception ex)
            {
                Status = CCDStatus.GrabError;
                if (ex.Message.Contains("device lost"))
                {
                    CloseCamera();
                }

                log.AddERRORLOG("抓取图像异常:" + ex.Message);
                return null;
            }
            finally
            {
                if (GCCount++ > 20)
                {
                    GC.Collect();
                    GCCount = 0;
                }
                Thread.Sleep(10);
                m_InGrabImage = false;
            }
        }

        /// <summary>
        /// 开始作业流程
        /// </summary>
        /// <param name="pcs">pcs数量</param>
        public void StartWork(int pcs)
        {
            //OpenCamera();
            GlobalVar.CCD_Worked = true;
            InitalControls();
            GlobalVar.CCD_Image.Clear();
            GlobalVar.CCD_Result.Clear();
            for (int i = 1; i <= pcs; i++)
            {
                GlobalVar.CCD_Image.Add(i, null);
                GlobalVar.CCD_Result.Add(i, 3);
            }
            BtnEnable(false);
        }

        private void BtnEnable(bool enable)
        {
            this.BeginInvoke(new Action(() =>
            {
                this.btn_CloseCamera.Enabled = enable;
                this.btn_Pause.Enabled = enable;
                this.btn_Save.Enabled = enable;
                this.btn_Start.Enabled = enable;
            }));
        }

        /// <summary>
        /// 停止作业流程
        /// </summary>
        public void EndWork()
        {
            GlobalVar.CCD_Worked = false;
            InitalControls();
            BtnEnable(true);
            // CloseCamera();
        }

        /// <summary>
        /// 解析条码【连续】
        /// </summary>
        /// <param name="m_ROI_image"></param>
        private void HandleBarcode(HObject m_ROI_image)
        {// Local iconic variables 

            HObject ho_Image, ho_Rectangle1, ho_Rectangle2;
            HObject ho_Mask1, ho_Mask2, ho_SymbolXLDs, ho_SymbolXLDs1;
            HObject ho_ImageZoom, ho_Image_out = null;

            // Local control variables 

            HTuple hv_Width = null, hv_Height = null, hv_success = null;
            HTuple hv_DataCodeHandle = null, hv_ResultHandles = null;
            HTuple hv_DecodedDataStrings = null, hv_ResultHandles1 = null;
            HTuple hv_DecodedDataStrings1 = null;
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Image);
            HOperatorSet.GenEmptyObj(out ho_Rectangle1);
            HOperatorSet.GenEmptyObj(out ho_Rectangle2);
            HOperatorSet.GenEmptyObj(out ho_Mask1);
            HOperatorSet.GenEmptyObj(out ho_Mask2);
            HOperatorSet.GenEmptyObj(out ho_ImageZoom);
            HOperatorSet.GenEmptyObj(out ho_SymbolXLDs);
            HOperatorSet.GenEmptyObj(out ho_SymbolXLDs1);

            ho_Image.Dispose();
            ho_Image = m_ROI_image;
            HOperatorSet.GetImageSize(ho_Image, out hv_Width, out hv_Height);

            ho_Rectangle1.Dispose();
            HOperatorSet.GenRectangle1(out ho_Rectangle1, 0, hv_Width / 2, hv_Height / 2, hv_Width);
            ho_Rectangle2.Dispose();
            HOperatorSet.GenRectangle1(out ho_Rectangle2, hv_Height / 2, hv_Width / 2, hv_Height, hv_Width);
            ho_Mask1.Dispose();
            HOperatorSet.ReduceDomain(ho_Image, ho_Rectangle1, out ho_Mask1);
            ho_Mask2.Dispose();
            HOperatorSet.ReduceDomain(ho_Image, ho_Rectangle2, out ho_Mask2);
            hv_success = 2;
            HOperatorSet.CreateDataCode2dModel("Data Matrix ECC 200", new HTuple(), new HTuple(),
                out hv_DataCodeHandle);
            ho_SymbolXLDs.Dispose();
            HOperatorSet.FindDataCode2d(ho_Mask1, out ho_SymbolXLDs, hv_DataCodeHandle, "train",
                "all", out hv_ResultHandles, out hv_DecodedDataStrings);

            ho_SymbolXLDs1.Dispose();
            HOperatorSet.FindDataCode2d(ho_Mask2, out ho_SymbolXLDs1, hv_DataCodeHandle,
                "train", "all", out hv_ResultHandles1, out hv_DecodedDataStrings1);
            if ((int)((new HTuple(hv_DecodedDataStrings.TupleEqual(new HTuple()))).TupleOr(
                new HTuple(hv_DecodedDataStrings1.TupleEqual(new HTuple())))) != 0)
            {
                hv_success = 0;
            }
            if ((int)(new HTuple(hv_success.TupleEqual(2))) != 0)
            {

                HOperatorSet.SetTposition(3600, hv_Height / 2, 12);
                HOperatorSet.WriteString(3600, "Barcode-2:" + hv_DecodedDataStrings1);

                HOperatorSet.SetTposition(3600, 24, 12);
                HOperatorSet.WriteString(3600, "Barcode-1:" + hv_DecodedDataStrings);

                this.BeginInvoke(new Action(() =>
                {
                    this.lb_UpBarcode.Text += hv_DecodedDataStrings;
                    this.lb_DownBarcode.Text += hv_DecodedDataStrings1;
                }));
            }
            else
            {
                HOperatorSet.WriteString(3600, new HTuple("Error,Find code Error"));
            }

            HOperatorSet.ClearAllDataCode2dModels();
            ho_Image.Dispose();
            ho_Rectangle1.Dispose();
            ho_Rectangle2.Dispose();
            ho_Mask1.Dispose();
            ho_Mask2.Dispose();
            ho_ImageZoom.Dispose();
            ho_SymbolXLDs.Dispose();
            ho_SymbolXLDs1.Dispose();
        }

        /// <summary>
        /// 解析条码【作业】
        /// </summary>
        /// <param name="m_ROI_image"></param>
        public string HandleBarcode(HObject m_ROI_image, int Index, string folder, out string barcode)
        {
            if (!GlobalVar.CCD_Image.Keys.Contains(Index)) GlobalVar.CCD_Image.Add(Index, null);
            if (!GlobalVar.CCD_Result.Keys.Contains(Index)) GlobalVar.CCD_Result.Add(Index, 3);
            // Local iconic variables 

            HObject ho_Image = null, ho_GrayImage = null, ho_Region = null;
            HObject ho_ConnectedRegions = null, ho_RegionFillUp = null;
            HObject ho_SelectedRegions = null, ho_ObjectSelected = null;
            HObject ho_Rectangle1 = null, ho_Rectangle2 = null, ho_Mask1 = null;
            HObject ho_Mask2 = null, ho_SymbolXLDs = null, ho_SymbolXLDs1 = null;


            // Local control variables 

            HTuple hv_ImageFiles = null, hv_Index = null;
            HTuple hv_Width = new HTuple(), hv_Height = new HTuple();
            HTuple hv_Number = new HTuple(), hv_upproduct = new HTuple();
            HTuple hv_downproduct = new HTuple(), hv_i = new HTuple();
            HTuple hv_Area = new HTuple(), hv_Row = new HTuple(), hv_Column = new HTuple();
            HTuple hv_success = new HTuple(), hv_DataCodeHandle = new HTuple();
            HTuple hv_DecodeDataStrings = new HTuple(), hv_DecodeDataStrings1 = new HTuple();
            HTuple hv_ResultHandles = new HTuple(), hv_DecodedDataStrings = new HTuple();
            HTuple hv_ResultHandles1 = new HTuple();

            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Image);
            HOperatorSet.GenEmptyObj(out ho_GrayImage);
            HOperatorSet.GenEmptyObj(out ho_Region);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions);
            HOperatorSet.GenEmptyObj(out ho_RegionFillUp);
            HOperatorSet.GenEmptyObj(out ho_SelectedRegions);
            HOperatorSet.GenEmptyObj(out ho_ObjectSelected);
            HOperatorSet.GenEmptyObj(out ho_Rectangle1);
            HOperatorSet.GenEmptyObj(out ho_Rectangle2);
            HOperatorSet.GenEmptyObj(out ho_Mask1);
            HOperatorSet.GenEmptyObj(out ho_Mask2);
            HOperatorSet.GenEmptyObj(out ho_SymbolXLDs);
            HOperatorSet.GenEmptyObj(out ho_SymbolXLDs1);


            ho_Image.Dispose();
            ho_Image = m_ROI_image;
            HOperatorSet.GetImageSize(ho_Image, out hv_Width, out hv_Height);
            ho_GrayImage.Dispose();
            HOperatorSet.Rgb1ToGray(ho_Image, out ho_GrayImage);
            ho_Region.Dispose();
            HOperatorSet.Threshold(ho_GrayImage, out ho_Region, 220, 255);
            ho_ConnectedRegions.Dispose();
            HOperatorSet.Connection(ho_Region, out ho_ConnectedRegions);
            ho_RegionFillUp.Dispose();
            HOperatorSet.FillUp(ho_ConnectedRegions, out ho_RegionFillUp);
            ho_SelectedRegions.Dispose();
            HOperatorSet.SelectShape(ho_ConnectedRegions, out ho_SelectedRegions, "area",
                "and", 9000, 25000);
            HOperatorSet.CountObj(ho_SelectedRegions, out hv_Number);
            hv_upproduct = 0;
            hv_downproduct = 0;
            HTuple end_val18 = hv_Number;
            HTuple step_val18 = 1;
            for (hv_i = 1; hv_i.Continue(end_val18, step_val18); hv_i = hv_i.TupleAdd(step_val18))
            {
                ho_ObjectSelected.Dispose();
                HOperatorSet.SelectObj(ho_SelectedRegions, out ho_ObjectSelected, hv_i);
                HOperatorSet.AreaCenter(ho_ObjectSelected, out hv_Area, out hv_Row, out hv_Column);
                if ((int)((new HTuple((new HTuple(hv_Row.TupleGreater(0))).TupleAnd(new HTuple(hv_Row.TupleLess(
                    hv_Height / 2))))).TupleAnd(new HTuple(hv_Column.TupleGreater(0)))) != 0)
                {
                    hv_upproduct = 1;
                }

                if ((int)((new HTuple((new HTuple(hv_Row.TupleGreater(hv_Height / 2))).TupleAnd(
                    new HTuple(hv_Row.TupleLess(hv_Height))))).TupleAnd(new HTuple(hv_Column.TupleGreater(
                    0)))) != 0)
                {
                    hv_downproduct = 1;
                }
            }
            ho_Rectangle1.Dispose();
            HOperatorSet.GenRectangle1(out ho_Rectangle1, 0, 0, hv_Height / 2, hv_Width/2);
            ho_Rectangle2.Dispose();
            HOperatorSet.GenRectangle1(out ho_Rectangle2, hv_Height / 2, 0, hv_Height, hv_Width/2);
            ho_Mask1.Dispose();
            HOperatorSet.ReduceDomain(ho_GrayImage, ho_Rectangle1, out ho_Mask1);
            ho_Mask2.Dispose();
            HOperatorSet.ReduceDomain(ho_GrayImage, ho_Rectangle2, out ho_Mask2);
            hv_success = 2;
            HOperatorSet.CreateDataCode2dModel("Data Matrix ECC 200", new HTuple(), new HTuple(),
                out hv_DataCodeHandle);
            //HOperatorSet.SetDataCode2dParam(hv_DataCodeHandle, "default_parameters", "standard_recognition");
            //HOperatorSet.SetDataCode2dParam(hv_DataCodeHandle, "persistence", 1);
            hv_DecodeDataStrings = new HTuple();
            hv_DecodeDataStrings1 = new HTuple();
            if ((int)(new HTuple(hv_upproduct.TupleNotEqual(0))) != 0)
            {
                //ho_SymbolXLDs.Dispose();
                //HOperatorSet.FindDataCode2d(ho_Mask1, out ho_SymbolXLDs, hv_DataCodeHandle,
                //    new HTuple(), new HTuple(), out hv_ResultHandles, out hv_DecodedDataStrings);
                //if ((int)(new HTuple(hv_DecodedDataStrings.TupleEqual(new HTuple()))) != 0)
                //{
                ho_SymbolXLDs.Dispose();
                HOperatorSet.FindDataCode2d(ho_Mask1, out ho_SymbolXLDs, hv_DataCodeHandle,
                    "train", "all", out hv_ResultHandles, out hv_DecodedDataStrings);
                //}
            }
            else
            {
                hv_DecodeDataStrings = new HTuple();
            }
            if ((int)(new HTuple(hv_downproduct.TupleNotEqual(0))) != 0)
            {
                //ho_SymbolXLDs1.Dispose();
                //HOperatorSet.FindDataCode2d(ho_Mask2, out ho_SymbolXLDs1, hv_DataCodeHandle,
                //    new HTuple(), new HTuple(), out hv_ResultHandles1, out hv_DecodeDataStrings1);
                //if ((int)(new HTuple(hv_DecodedDataStrings.TupleEqual(new HTuple()))) != 0)
                //{
                ho_SymbolXLDs1.Dispose();
                HOperatorSet.FindDataCode2d(ho_Mask2, out ho_SymbolXLDs1, hv_DataCodeHandle,
                    "train", "all", out hv_ResultHandles1, out hv_DecodeDataStrings1);
                //  }
            }
            else
            {
                hv_DecodeDataStrings1 = new HTuple();
            }

            if ((int)(new HTuple(hv_DecodedDataStrings.TupleNotEqual(new HTuple()))) != 0)
            {
                HOperatorSet.SetTposition(3600, hv_Height / 5, 12);
                HOperatorSet.WriteString(3600, "Barcode-1:" + hv_DecodedDataStrings);
            }
            else
            {
                HOperatorSet.SetTposition(3600, hv_Height / 5, 12);
                HOperatorSet.WriteString(3600, new HTuple("Error,Find code Error"));
                hv_DecodedDataStrings = "null";
            }

            if ((int)(new HTuple(hv_DecodeDataStrings1.TupleNotEqual(new HTuple()))) != 0)
            {
                HOperatorSet.SetTposition(3600, (3 * hv_Height) / 5, 12);
                HOperatorSet.WriteString(3600, "Barcode-2:" + hv_DecodeDataStrings1);
            }
            else
            {
                HOperatorSet.SetTposition(3600, (3 * hv_Height) / 5, 12);
                HOperatorSet.WriteString(3600, new HTuple("Error,Find code Error"));
                hv_DecodeDataStrings1 = "null";
            }
            this.BeginInvoke(new Action(() =>
            {
                this.lb_UpBarcode.Text = "上条码：" + hv_DecodedDataStrings;
                this.lb_DownBarcode.Text = "下条码：" + hv_DecodeDataStrings1;
                this.lb_DownBarcode.Update();
                this.lb_UpBarcode.Update();
            }));
            barcode = hv_DecodedDataStrings + "|" + hv_DecodeDataStrings1;
            if (hv_DecodedDataStrings != "null" && hv_DecodeDataStrings1 != "null")
                GlobalVar.CCD_Result[Index] = 1;
            else GlobalVar.CCD_Result[Index] = 2;

            string file = folder + "/" + DateTime.Now.ToString("yyyyMMdd") + "/";
            if (!Directory.Exists(file)) Directory.CreateDirectory(file);
            //file += DateTime.Now.ToString("HH") + "/"; 
            //if (!Directory.Exists(file)) Directory.CreateDirectory(file);
            string fileName = file + (Index + 1) + "_上" + hv_DecodedDataStrings + "_下" + hv_DecodeDataStrings1 + "_" + DateTime.Now.ToString("HHmmss") + ".jpeg";
            HOperatorSet.WriteImage(ho_Image, "jpeg", 0, fileName);
            //if (ho_Image != null)
            // HOperatorSet.WriteImage(ho_Image, "jpeg", 0, ((("F:/IC_TestData/上_" + hv_DecodedDataStrings) + "_下_") + hv_DecodeDataStrings1) + ".jpg");
            // else log.AddERRORLOG("图片为空");
            //GlobalVar.CCD_Image[Index] = HObjectToGrayBitmap(ho_Image);

            HOperatorSet.ClearDataCode2dModel(hv_DataCodeHandle);


            ho_Image.Dispose();
            ho_GrayImage.Dispose();
            ho_Region.Dispose();
            ho_ConnectedRegions.Dispose();
            ho_RegionFillUp.Dispose();
            ho_SelectedRegions.Dispose();
            ho_ObjectSelected.Dispose();
            ho_Rectangle1.Dispose();
            ho_Rectangle2.Dispose();
            ho_Mask1.Dispose();
            ho_Mask2.Dispose();
            ho_SymbolXLDs.Dispose();
            ho_SymbolXLDs1.Dispose();

            return barcode;
        }

        // Procedures 
        // Chapter: Graphics / Text
        // Short Description: Set font independent of OS 
        public void set_display_font(HTuple hv_WindowHandle, HTuple hv_Size, HTuple hv_Font,
            HTuple hv_Bold, HTuple hv_Slant)
        {
            // Local iconic variables 

            // Local control variables 

            HTuple hv_OS = null, hv_BufferWindowHandle = new HTuple();
            HTuple hv_Ascent = new HTuple(), hv_Descent = new HTuple();
            HTuple hv_Width = new HTuple(), hv_Height = new HTuple();
            HTuple hv_Scale = new HTuple(), hv_Exception = new HTuple();
            HTuple hv_SubFamily = new HTuple(), hv_Fonts = new HTuple();
            HTuple hv_SystemFonts = new HTuple(), hv_Guess = new HTuple();
            HTuple hv_I = new HTuple(), hv_Index = new HTuple(), hv_AllowedFontSizes = new HTuple();
            HTuple hv_Distances = new HTuple(), hv_Indices = new HTuple();
            HTuple hv_FontSelRegexp = new HTuple(), hv_FontsCourier = new HTuple();
            HTuple hv_Bold_COPY_INP_TMP = hv_Bold.Clone();
            HTuple hv_Font_COPY_INP_TMP = hv_Font.Clone();
            HTuple hv_Size_COPY_INP_TMP = hv_Size.Clone();
            HTuple hv_Slant_COPY_INP_TMP = hv_Slant.Clone();

            // Initialize local and output iconic variables 
            //This procedure sets the text font of the current window with
            //the specified attributes.
            //It is assumed that following fonts are installed on the system:
            //Windows: Courier New, Arial Times New Roman
            //Mac OS X: CourierNewPS, Arial, TimesNewRomanPS
            //Linux: courier, helvetica, times
            //Because fonts are displayed smaller on Linux than on Windows,
            //a scaling factor of 1.25 is used the get comparable results.
            //For Linux, only a limited number of font sizes is supported,
            //to get comparable results, it is recommended to use one of the
            //following sizes: 9, 11, 14, 16, 20, 27
            //(which will be mapped internally on Linux systems to 11, 14, 17, 20, 25, 34)
            //
            //Input parameters:
            //WindowHandle: The graphics window for which the font will be set
            //Size: The font size. If Size=-1, the default of 16 is used.
            //Bold: If set to 'true', a bold font is used
            //Slant: If set to 'true', a slanted font is used
            //
            HOperatorSet.GetSystem("operating_system", out hv_OS);
            // dev_get_preferences(...); only in hdevelop
            // dev_set_preferences(...); only in hdevelop
            if ((int)((new HTuple(hv_Size_COPY_INP_TMP.TupleEqual(new HTuple()))).TupleOr(
                new HTuple(hv_Size_COPY_INP_TMP.TupleEqual(-1)))) != 0)
            {
                hv_Size_COPY_INP_TMP = 16;
            }
            if ((int)(new HTuple(((hv_OS.TupleSubstr(0, 2))).TupleEqual("Win"))) != 0)
            {
                //Set font on Windows systems
                try
                {
                    //Check, if font scaling is switched on
                    HOperatorSet.OpenWindow(0, 0, 256, 256, 0, "buffer", "", out hv_BufferWindowHandle);
                    HOperatorSet.SetFont(hv_BufferWindowHandle, "-Consolas-16-*-0-*-*-1-");
                    HOperatorSet.GetStringExtents(hv_BufferWindowHandle, "test_string", out hv_Ascent,
                        out hv_Descent, out hv_Width, out hv_Height);
                    //Expected width is 110
                    hv_Scale = 110.0 / hv_Width;
                    hv_Size_COPY_INP_TMP = ((hv_Size_COPY_INP_TMP * hv_Scale)).TupleInt();
                    HOperatorSet.CloseWindow(hv_BufferWindowHandle);
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    //throw (Exception)
                }
                if ((int)((new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("Courier"))).TupleOr(
                    new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("courier")))) != 0)
                {
                    hv_Font_COPY_INP_TMP = "Courier New";
                }
                else if ((int)(new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("mono"))) != 0)
                {
                    hv_Font_COPY_INP_TMP = "Consolas";
                }
                else if ((int)(new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("sans"))) != 0)
                {
                    hv_Font_COPY_INP_TMP = "Arial";
                }
                else if ((int)(new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("serif"))) != 0)
                {
                    hv_Font_COPY_INP_TMP = "Times New Roman";
                }
                if ((int)(new HTuple(hv_Bold_COPY_INP_TMP.TupleEqual("true"))) != 0)
                {
                    hv_Bold_COPY_INP_TMP = 1;
                }
                else if ((int)(new HTuple(hv_Bold_COPY_INP_TMP.TupleEqual("false"))) != 0)
                {
                    hv_Bold_COPY_INP_TMP = 0;
                }
                else
                {
                    hv_Exception = "Wrong value of control parameter Bold";
                    throw new HalconException(hv_Exception);
                }
                if ((int)(new HTuple(hv_Slant_COPY_INP_TMP.TupleEqual("true"))) != 0)
                {
                    hv_Slant_COPY_INP_TMP = 1;
                }
                else if ((int)(new HTuple(hv_Slant_COPY_INP_TMP.TupleEqual("false"))) != 0)
                {
                    hv_Slant_COPY_INP_TMP = 0;
                }
                else
                {
                    hv_Exception = "Wrong value of control parameter Slant";
                    throw new HalconException(hv_Exception);
                }
                try
                {
                    HOperatorSet.SetFont(hv_WindowHandle, ((((((("-" + hv_Font_COPY_INP_TMP) + "-") + hv_Size_COPY_INP_TMP) + "-*-") + hv_Slant_COPY_INP_TMP) + "-*-*-") + hv_Bold_COPY_INP_TMP) + "-");
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    //throw (Exception)
                }
            }
            else if ((int)(new HTuple(((hv_OS.TupleSubstr(0, 2))).TupleEqual("Dar"))) != 0)
            {
                //Set font on Mac OS X systems. Since OS X does not have a strict naming
                //scheme for font attributes, we use tables to determine the correct font
                //name.
                hv_SubFamily = 0;
                if ((int)(new HTuple(hv_Slant_COPY_INP_TMP.TupleEqual("true"))) != 0)
                {
                    hv_SubFamily = hv_SubFamily.TupleBor(1);
                }
                else if ((int)(new HTuple(hv_Slant_COPY_INP_TMP.TupleNotEqual("false"))) != 0)
                {
                    hv_Exception = "Wrong value of control parameter Slant";
                    throw new HalconException(hv_Exception);
                }
                if ((int)(new HTuple(hv_Bold_COPY_INP_TMP.TupleEqual("true"))) != 0)
                {
                    hv_SubFamily = hv_SubFamily.TupleBor(2);
                }
                else if ((int)(new HTuple(hv_Bold_COPY_INP_TMP.TupleNotEqual("false"))) != 0)
                {
                    hv_Exception = "Wrong value of control parameter Bold";
                    throw new HalconException(hv_Exception);
                }
                if ((int)(new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("mono"))) != 0)
                {
                    hv_Fonts = new HTuple();
                    hv_Fonts[0] = "Menlo-Regular";
                    hv_Fonts[1] = "Menlo-Italic";
                    hv_Fonts[2] = "Menlo-Bold";
                    hv_Fonts[3] = "Menlo-BoldItalic";
                }
                else if ((int)((new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("Courier"))).TupleOr(
                    new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("courier")))) != 0)
                {
                    hv_Fonts = new HTuple();
                    hv_Fonts[0] = "CourierNewPSMT";
                    hv_Fonts[1] = "CourierNewPS-ItalicMT";
                    hv_Fonts[2] = "CourierNewPS-BoldMT";
                    hv_Fonts[3] = "CourierNewPS-BoldItalicMT";
                }
                else if ((int)(new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("sans"))) != 0)
                {
                    hv_Fonts = new HTuple();
                    hv_Fonts[0] = "ArialMT";
                    hv_Fonts[1] = "Arial-ItalicMT";
                    hv_Fonts[2] = "Arial-BoldMT";
                    hv_Fonts[3] = "Arial-BoldItalicMT";
                }
                else if ((int)(new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("serif"))) != 0)
                {
                    hv_Fonts = new HTuple();
                    hv_Fonts[0] = "TimesNewRomanPSMT";
                    hv_Fonts[1] = "TimesNewRomanPS-ItalicMT";
                    hv_Fonts[2] = "TimesNewRomanPS-BoldMT";
                    hv_Fonts[3] = "TimesNewRomanPS-BoldItalicMT";
                }
                else
                {
                    //Attempt to figure out which of the fonts installed on the system
                    //the user could have meant.
                    HOperatorSet.QueryFont(hv_WindowHandle, out hv_SystemFonts);
                    hv_Fonts = new HTuple();
                    hv_Fonts = hv_Fonts.TupleConcat(hv_Font_COPY_INP_TMP);
                    hv_Fonts = hv_Fonts.TupleConcat(hv_Font_COPY_INP_TMP);
                    hv_Fonts = hv_Fonts.TupleConcat(hv_Font_COPY_INP_TMP);
                    hv_Fonts = hv_Fonts.TupleConcat(hv_Font_COPY_INP_TMP);
                    hv_Guess = new HTuple();
                    hv_Guess = hv_Guess.TupleConcat(hv_Font_COPY_INP_TMP);
                    hv_Guess = hv_Guess.TupleConcat(hv_Font_COPY_INP_TMP + "-Regular");
                    hv_Guess = hv_Guess.TupleConcat(hv_Font_COPY_INP_TMP + "MT");
                    for (hv_I = 0; (int)hv_I <= (int)((new HTuple(hv_Guess.TupleLength())) - 1); hv_I = (int)hv_I + 1)
                    {
                        HOperatorSet.TupleFind(hv_SystemFonts, hv_Guess.TupleSelect(hv_I), out hv_Index);
                        if ((int)(new HTuple(hv_Index.TupleNotEqual(-1))) != 0)
                        {
                            if (hv_Fonts == null)
                                hv_Fonts = new HTuple();
                            hv_Fonts[0] = hv_Guess.TupleSelect(hv_I);
                            break;
                        }
                    }
                    //Guess name of slanted font
                    hv_Guess = new HTuple();
                    hv_Guess = hv_Guess.TupleConcat(hv_Font_COPY_INP_TMP + "-Italic");
                    hv_Guess = hv_Guess.TupleConcat(hv_Font_COPY_INP_TMP + "-ItalicMT");
                    hv_Guess = hv_Guess.TupleConcat(hv_Font_COPY_INP_TMP + "-Oblique");
                    for (hv_I = 0; (int)hv_I <= (int)((new HTuple(hv_Guess.TupleLength())) - 1); hv_I = (int)hv_I + 1)
                    {
                        HOperatorSet.TupleFind(hv_SystemFonts, hv_Guess.TupleSelect(hv_I), out hv_Index);
                        if ((int)(new HTuple(hv_Index.TupleNotEqual(-1))) != 0)
                        {
                            if (hv_Fonts == null)
                                hv_Fonts = new HTuple();
                            hv_Fonts[1] = hv_Guess.TupleSelect(hv_I);
                            break;
                        }
                    }
                    //Guess name of bold font
                    hv_Guess = new HTuple();
                    hv_Guess = hv_Guess.TupleConcat(hv_Font_COPY_INP_TMP + "-Bold");
                    hv_Guess = hv_Guess.TupleConcat(hv_Font_COPY_INP_TMP + "-BoldMT");
                    for (hv_I = 0; (int)hv_I <= (int)((new HTuple(hv_Guess.TupleLength())) - 1); hv_I = (int)hv_I + 1)
                    {
                        HOperatorSet.TupleFind(hv_SystemFonts, hv_Guess.TupleSelect(hv_I), out hv_Index);
                        if ((int)(new HTuple(hv_Index.TupleNotEqual(-1))) != 0)
                        {
                            if (hv_Fonts == null)
                                hv_Fonts = new HTuple();
                            hv_Fonts[2] = hv_Guess.TupleSelect(hv_I);
                            break;
                        }
                    }
                    //Guess name of bold slanted font
                    hv_Guess = new HTuple();
                    hv_Guess = hv_Guess.TupleConcat(hv_Font_COPY_INP_TMP + "-BoldItalic");
                    hv_Guess = hv_Guess.TupleConcat(hv_Font_COPY_INP_TMP + "-BoldItalicMT");
                    hv_Guess = hv_Guess.TupleConcat(hv_Font_COPY_INP_TMP + "-BoldOblique");
                    for (hv_I = 0; (int)hv_I <= (int)((new HTuple(hv_Guess.TupleLength())) - 1); hv_I = (int)hv_I + 1)
                    {
                        HOperatorSet.TupleFind(hv_SystemFonts, hv_Guess.TupleSelect(hv_I), out hv_Index);
                        if ((int)(new HTuple(hv_Index.TupleNotEqual(-1))) != 0)
                        {
                            if (hv_Fonts == null)
                                hv_Fonts = new HTuple();
                            hv_Fonts[3] = hv_Guess.TupleSelect(hv_I);
                            break;
                        }
                    }
                }
                hv_Font_COPY_INP_TMP = hv_Fonts.TupleSelect(hv_SubFamily);
                try
                {
                    HOperatorSet.SetFont(hv_WindowHandle, (hv_Font_COPY_INP_TMP + "-") + hv_Size_COPY_INP_TMP);
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    //throw (Exception)
                }
            }
            else
            {
                //Set font for UNIX systems
                hv_Size_COPY_INP_TMP = hv_Size_COPY_INP_TMP * 1.25;
                hv_AllowedFontSizes = new HTuple();
                hv_AllowedFontSizes[0] = 11;
                hv_AllowedFontSizes[1] = 14;
                hv_AllowedFontSizes[2] = 17;
                hv_AllowedFontSizes[3] = 20;
                hv_AllowedFontSizes[4] = 25;
                hv_AllowedFontSizes[5] = 34;
                if ((int)(new HTuple(((hv_AllowedFontSizes.TupleFind(hv_Size_COPY_INP_TMP))).TupleEqual(
                    -1))) != 0)
                {
                    hv_Distances = ((hv_AllowedFontSizes - hv_Size_COPY_INP_TMP)).TupleAbs();
                    HOperatorSet.TupleSortIndex(hv_Distances, out hv_Indices);
                    hv_Size_COPY_INP_TMP = hv_AllowedFontSizes.TupleSelect(hv_Indices.TupleSelect(
                        0));
                }
                if ((int)((new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("mono"))).TupleOr(new HTuple(hv_Font_COPY_INP_TMP.TupleEqual(
                    "Courier")))) != 0)
                {
                    hv_Font_COPY_INP_TMP = "courier";
                }
                else if ((int)(new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("sans"))) != 0)
                {
                    hv_Font_COPY_INP_TMP = "helvetica";
                }
                else if ((int)(new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("serif"))) != 0)
                {
                    hv_Font_COPY_INP_TMP = "times";
                }
                if ((int)(new HTuple(hv_Bold_COPY_INP_TMP.TupleEqual("true"))) != 0)
                {
                    hv_Bold_COPY_INP_TMP = "bold";
                }
                else if ((int)(new HTuple(hv_Bold_COPY_INP_TMP.TupleEqual("false"))) != 0)
                {
                    hv_Bold_COPY_INP_TMP = "medium";
                }
                else
                {
                    hv_Exception = "Wrong value of control parameter Bold";
                    throw new HalconException(hv_Exception);
                }
                if ((int)(new HTuple(hv_Slant_COPY_INP_TMP.TupleEqual("true"))) != 0)
                {
                    if ((int)(new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("times"))) != 0)
                    {
                        hv_Slant_COPY_INP_TMP = "i";
                    }
                    else
                    {
                        hv_Slant_COPY_INP_TMP = "o";
                    }
                }
                else if ((int)(new HTuple(hv_Slant_COPY_INP_TMP.TupleEqual("false"))) != 0)
                {
                    hv_Slant_COPY_INP_TMP = "r";
                }
                else
                {
                    hv_Exception = "Wrong value of control parameter Slant";
                    throw new HalconException(hv_Exception);
                }
                try
                {
                    HOperatorSet.SetFont(hv_WindowHandle, ((((((("-adobe-" + hv_Font_COPY_INP_TMP) + "-") + hv_Bold_COPY_INP_TMP) + "-") + hv_Slant_COPY_INP_TMP) + "-normal-*-") + hv_Size_COPY_INP_TMP) + "-*-*-*-*-*-*-*");
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    if ((int)((new HTuple(((hv_OS.TupleSubstr(0, 4))).TupleEqual("Linux"))).TupleAnd(
                        new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("courier")))) != 0)
                    {
                        HOperatorSet.QueryFont(hv_WindowHandle, out hv_Fonts);
                        hv_FontSelRegexp = (("^-[^-]*-[^-]*[Cc]ourier[^-]*-" + hv_Bold_COPY_INP_TMP) + "-") + hv_Slant_COPY_INP_TMP;
                        hv_FontsCourier = ((hv_Fonts.TupleRegexpSelect(hv_FontSelRegexp))).TupleRegexpMatch(
                            hv_FontSelRegexp);
                        if ((int)(new HTuple((new HTuple(hv_FontsCourier.TupleLength())).TupleEqual(
                            0))) != 0)
                        {
                            hv_Exception = "Wrong font name";
                            //throw (Exception)
                        }
                        else
                        {
                            try
                            {
                                HOperatorSet.SetFont(hv_WindowHandle, (((hv_FontsCourier.TupleSelect(
                                    0)) + "-normal-*-") + hv_Size_COPY_INP_TMP) + "-*-*-*-*-*-*-*");
                            }
                            // catch (Exception) 
                            catch (HalconException HDevExpDefaultException2)
                            {
                                HDevExpDefaultException2.ToHTuple(out hv_Exception);
                                //throw (Exception)
                            }
                        }
                    }
                    //throw (Exception)
                }
            }
            // dev_set_preferences(...); only in hdevelop

            return;
        }


        #region 图像转换
        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = true)]
        internal static extern void CopyMemory(int Destination, int Source, int Length);
        /// <summary>
        /// HObject转bmp
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public Bitmap HObjectToGrayBitmap(HObject image)//异常信息: System.AccessViolationException  慎用！！！
        {
            Bitmap res;
            try
            {
                HTuple hpoint, type, width, height;

                const int Alpha = 255;
                int[] ptr = new int[2];
                HOperatorSet.GetImagePointer1(image, out hpoint, out type, out width, out height);

                res = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
                ColorPalette pal = res.Palette;
                for (int i = 0; i <= 255; i++)
                {
                    pal.Entries[i] = Color.FromArgb(Alpha, i, i, i);
                }
                res.Palette = pal;
                Rectangle rect = new Rectangle(0, 0, width, height);
                BitmapData bitmapData = res.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
                int PixelSize = Bitmap.GetPixelFormatSize(bitmapData.PixelFormat) / 8;
                ptr[0] = bitmapData.Scan0.ToInt32();
                ptr[1] = hpoint.I;
                if (width % 4 == 0)
                    CopyMemory(ptr[0], ptr[1], width * height * PixelSize);
                else
                {
                    for (int i = 0; i < height - 1; i++)
                    {
                        ptr[1] += width;
                        CopyMemory(ptr[0], ptr[1], width * PixelSize);
                        ptr[0] += bitmapData.Stride;
                    }
                }
                res.UnlockBits(bitmapData);
                return res;
            }
            catch { return null; }
            finally { GC.Collect(); }
        }

        public HObject HImageConvertFromBitmap(Bitmap bmp)
        {
            HObject ho_image;
            HOperatorSet.GenEmptyObj(out ho_image);
            BitmapData bmpdata = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            HOperatorSet.GenImageInterleaved(out ho_image, bmpdata.Scan0, "bgrx", bmp.Width, bmp.Height, -1, "byte", bmp.Width, bmp.Height, 0, 0, -1, 0);
            return ho_image;
        }
        #endregion
        /// <summary>
        /// 直接显示该图片
        /// </summary>
        /// <param name="ho_image"></param>
        public void DirectShowImage(HObject ho_image)
        {
            HImage image = new HImage(ho_image);
            resetWindow(image);
            repaint(this.hWindowControl_Player.HalconWindow, image);
            Clear();
        }

        /// <summary>
        /// 清除结果
        /// </summary>
        internal void Clear()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(delegate { Clear(); }));
            }
            else
            {
                //this.lisView_Result.Clear();
                this.lb_DownBarcode.Text = "下条码：";
                this.lb_UpBarcode.Text = "上条码：";
            }
        }


        /// <summary>
        /// Repaints the HALCON window 'window'
        /// </summary>
        public void repaint(HalconDotNet.HWindow window, HObject obj)
        {
            HSystem.SetSystem("flush_graphic", "false");
            window.ClearWindow();

            window.DispObj(obj);

            HSystem.SetSystem("flush_graphic", "true");

            window.SetColor("black");
            window.DispLine(-100.0, -100.0, -101.0, -101.0);
        }

        /* Image coordinates, which describe the image part that is displayed  
        in the HALCON window */
        private double ImgRow1, ImgCol1, ImgRow2, ImgCol2;
        private double zoomWndFactor;
        public void resetWindow(HObject obj, double imageHeight = 512, double imageWidth = 512)
        {
            string s = string.Empty;
            int h, w;
            ((HImage)obj).GetImagePointer1(out s, out w, out h);

            ImgRow1 = 0;
            ImgCol1 = 0;
            ImgRow2 = imageHeight = h;
            ImgCol2 = imageWidth = w;

            zoomWndFactor = (double)imageWidth / hWindowControl_Player.Width;

            System.Drawing.Rectangle rect = hWindowControl_Player.ImagePart;
            rect.X = (int)ImgCol1;
            rect.Y = (int)ImgRow1;
            rect.Width = (int)imageWidth;
            rect.Height = (int)imageHeight;
            hWindowControl_Player.ImagePart = rect;
        }

        /// <summary>
        /// 开启相机【返回相机是否开启成功】
        /// </summary>
        public bool OpenCamera()
        {
            int TryCount = 0;
            bool result = false;
            do
            {
                if (!m_OpenCamera()) TryCount++;
                else
                {
                    result = true;
                    break;
                }
            }
            while (TryCount <= 3);

            if (!result) Status = CCDStatus.OpenError;//三次开启失败才认为开启失败

            return result;
        }

        private void btn_CloseCamera_Click(object sender, EventArgs e)
        {
            if (Player.IsSet) Player.Reset();
            CloseCamera();//关闭相机
        }

        private void btn_Pause_Click(object sender, EventArgs e)
        {
            PlayerPause();
        }

        private void btn_Save_Click(object sender, EventArgs e)
        {
            try
            {
                SaveImage(Grab_Image, this.GrabMode);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "保存图片异常");
            }
        }

        /// <summary>
        /// 保存图片
        /// </summary>
        /// <param name="grab_Image"></param>
        /// <param name="grabMode"></param>
        private void SaveImage(HObject grab_Image, int grabMode)
        {
            FolderBrowserDialog folder = new FolderBrowserDialog();
            folder.RootFolder = Environment.SpecialFolder.Desktop;
            folder.Description = "选择保存文件夹";
            folder.ShowNewFolderButton = true;
            if (folder.ShowDialog() == DialogResult.OK)
            {
                HOperatorSet.WriteImage(grab_Image, "tiff", 0, string.Format(@"{0}\{1}_{2}", folder.SelectedPath, grabMode, DateTime.Now.ToString("yyyyMMdd_HHmmss")));
            }
        }

        /// <summary>

        /// 开启相机
        /// </summary>
        /// <returns></returns>
        internal bool m_OpenCamera()
        {
            try
            {
                if (FramegrabberIsOpen) return true;
                FramegrabberIsOpen = false;

                // Initialize local and output iconic variables 
                HOperatorSet.GenEmptyObj(out Grab_Image);
                if (ConnectMode == "GigE")
                    //Image Acquisition 01: Code generated by Image Acquisition 01
                    HOperatorSet.OpenFramegrabber("GigEVision", 0, 0, 0, 0, 0, 0, "progressive",
                    -1, "default", -1, "false", "default", this.CameraName, 0, -1, out hv_AcqHandle);
                else if (ConnectMode == "DirectShow")
                    HOperatorSet.OpenFramegrabber("DirectShow", 1, 1, 0, 0, 0, 0, "default", 8, "rgb",
                        -1, "false", "default", "[0] Basler GenICam Source", 0, -1, out hv_AcqHandle);

                FramegrabberIsOpen = true;

                HOperatorSet.GrabImageStart(hv_AcqHandle, -1);
                HOperatorSet.GrabImageAsync(out Grab_Image, hv_AcqHandle, -1);

                Status = CCDStatus.Online;

                HTuple value;
                HOperatorSet.GetFramegrabberParam(hv_AcqHandle, "revision", out value);
            }
            catch (Exception ex)
            {
                log.AddERRORLOG(string.Format("{0}\t开启相机异常:{1}", this.m_CameraName.ToString(), ex.Message));
            }

            return FramegrabberIsOpen;
        }



        private void GrabImageBeat()
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// 关闭相机
        /// </summary>
        public void CloseCamera()
        {
            try
            {
                HOperatorSet.CloseFramegrabber(hv_AcqHandle);

                Grab_Image.Dispose();
                Status = CCDStatus.Offline;
            }
            catch (Exception ex)
            {
                log.AddERRORLOG(string.Format("{0} CloseCamera Err:{1}", this.m_CameraName, ex.Message));
                Status = CCDStatus.CloseError;
            }
            finally
            {
                FramegrabberIsOpen = false;
            }
        }
    }
}
