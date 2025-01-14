﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Advantech.Motion;

namespace OQC_IC_CHECK_System
{
    partial class PCI1285_E
    {
        DEV_LIST[] CurAvaliableDevs = new DEV_LIST[Motion.MAX_DEVICES];//可以获得的数量
        uint deviceCount = 0;//数量
        uint DeviceNum = 0;//编号
        IntPtr m_DeviceHandle = IntPtr.Zero;

        IntPtr[] m_Axishand = new IntPtr[32];
        uint m_ulAxisCount = 0;//轴数量
        /// <summary>
        /// 几个轴
        /// </summary>
        public uint AxisCount { get { return m_ulAxisCount; } }

        IntPtr m_GpHand = IntPtr.Zero;  // 
        uint AxCountInGp = 0;    //群组中轴的数量

        bool m_bInit = false;//板卡是否开启
        /// <summary>
        /// 板卡是否开启
        /// </summary>
        internal bool Enable { get { return m_bInit; } }
        /// <summary>
        /// 当前轴群组状态
        /// </summary>
        public ushort m_GpAxisStatus = 0;
        //群组中轴的距离阵列，阵列元素的每个值都表示轴的相对位置。
        double[] EndArray = new double[32];

        #region 事件
        public delegate void dele_UpdatePosition(double position);
        public delegate void dele_EMGStop();
        /// <summary>
        /// 更新X轴坐标--IC
        /// </summary>
        public event dele_UpdatePosition Event_UpdatePositionX;
        /// <summary>
        /// 更新Y轴坐标--IC
        /// </summary>
        public event dele_UpdatePosition Event_UpdatePositionY;
        /// <summary>
        /// 更新Z轴坐标--CCD相机轴
        /// </summary>
        public event dele_UpdatePosition Event_UpdatePositionZ;
        /// <summary>
        /// 更新A轴坐标--上料轴
        /// </summary>
        public event dele_UpdatePosition Event_UpdatePositionA;
        /// <summary>
        /// 更新C轴坐标--IC上料
        /// </summary>
        public event dele_UpdatePosition Event_UpdatePositionC;
        /// <summary>
        /// 更新D轴坐标--FPC上料
        /// </summary>
        public event dele_UpdatePosition Event_UpdatePositionD;
        /// <summary>
        /// 更新B轴坐标--下料轴
        /// </summary>
        public event dele_UpdatePosition Event_UpdatePositionB;
        /// <summary>
        /// 紧急停止
        /// </summary>
        public event dele_EMGStop Event_EMGStop;
        #endregion
        #region 轴位置
        private double m_CmdPosition_X = 0.000d;//脉冲数--x轴
        private double m_CmdPosition_Y = 0.000d;//脉冲数--y轴
        private double m_CmdPosition_Z = 0.000d;//脉冲数--相机轴
        private double m_CmdPosition_C = 0.000d;//脉冲数--IC上料轴
        private double m_CmdPosition_D = 0.000d;//脉冲数--pcs上料轴
        private double m_CmdPosition_B = 0.000d;//脉冲数--下料轴
        private double m_CmdPosition_A = 0.000d;//脉冲数--上料轴
        /// <summary>
        /// x轴实际位置【已经转换】
        /// </summary>
        internal double Position_X
        {
            set
            {
                if (value != m_CmdPosition_X)
                {
                    if (!GlobalVar.Machine.Pause)//--仅在正常作业情况下检测[20180910 lqz]
                        if (!(Tag_Lock1.CurrentValue && Tag_Lock2.CurrentValue && Tag_LockBefore.CurrentValue)) StopMove(GlobalVar.AxisX.LinkIndex);//门未关好，禁止移动
                    if (!Tag_LightSensor.CurrentValue) StopMove(GlobalVar.AxisX.LinkIndex);//X轴在处理PCS异常时可以动作
                    UpdatePositionX(value / GlobalVar.ServCMDRate);
                }
                m_CmdPosition_X = value;
            }
            get { return (double)m_CmdPosition_X / GlobalVar.ServCMDRate; }
        }
        /// <summary>
        /// y轴实际位置【已经转换】
        /// </summary>
        internal double Position_Y
        {
            set
            {
                if (value != m_CmdPosition_Y)
                {
                    if (!GlobalVar.Machine.Pause)//--仅在正常作业情况下检测[20180910 lqz]
                        if (!(Tag_Lock1.CurrentValue && Tag_Lock2.CurrentValue && Tag_LockBefore.CurrentValue)) StopMove(GlobalVar.AxisY.LinkIndex);//门未关好，禁止移动
                    if (!Tag_LightSensor.CurrentValue) StopMove(GlobalVar.AxisY.LinkIndex);//Y轴在处理PCS异常时可以动作
                    UpdatePositionY(value / GlobalVar.ServCMDRate);
                }
                m_CmdPosition_Y = value;
            }
            get { return (double)m_CmdPosition_Y / GlobalVar.ServCMDRate; }
        }
        /// <summary>
        /// z轴实际位置【已经转换】
        /// </summary>
        internal double Position_Z
        {
            set
            {
                if (m_CmdPosition_Z != value)
                {
                    UpdatePositionZ(value / GlobalVar.MotorRate);
                    myfunction.WriteIniString(GlobalVar.gl_inisection_SoftWare, GlobalVar.gl_iniKey_ZPosition, value.ToString("F3"));
                }

                m_CmdPosition_Z = value;
                //GlobalVar.BPosition = m_CmdPosition_B * GlobalVar.MotorBRate;
            }
            get { return (double)m_CmdPosition_Z / GlobalVar.MotorRate; }
        }
        /// <summary>
        /// A轴实际位置【已经转换】
        /// </summary>
        internal double Position_A
        {
            set
            {
                if (value != m_CmdPosition_A)
                {
                    if (!GlobalVar.Machine.Pause)//--仅在正常作业情况下检测[20180910 lqz]
                    {
                        if (!(Tag_Lock1.CurrentValue && Tag_Lock2.CurrentValue && Tag_LockBefore.CurrentValue && Tag_LightSensor.CurrentValue)) StopMove(GlobalVar.AxisA.LinkIndex);//门未关好，禁止移动
                    }
                    // if (Position_B < GlobalVar.DropSaveDistance&&Target_A>GlobalVar.FeedSaveDistance) StopMove(GlobalVar.AxisA.LinkIndex);
                    UpdatePositionA(value / GlobalVar.ServCMDRate);
                }
                m_CmdPosition_A = value;
            }
            get { return (double)m_CmdPosition_A / GlobalVar.ServCMDRate; }
        }
        /// <summary>
        /// B轴实际位置【已经转换】
        /// </summary>
        internal double Position_B
        {
            set
            {
                if (value != m_CmdPosition_B)
                {
                    if (!GlobalVar.Machine.Pause)//--仅在正常作业情况下检测[20180910 lqz]
                        if (!(Tag_Lock1.CurrentValue && Tag_Lock2.CurrentValue && Tag_LockBefore.CurrentValue && Tag_LightSensor.CurrentValue)) StopMove(GlobalVar.AxisB.LinkIndex);//门未关好，禁止移动
                   // if (Position_A > GlobalVar.FeedSaveDistance&&Target_B<GlobalVar.DropSaveDistance) StopMove(GlobalVar.AxisB.LinkIndex);
                    UpdatePositionB(value / GlobalVar.ServCMDRate);
                }
                m_CmdPosition_B = value;
            }
            get { return (double)m_CmdPosition_B / GlobalVar.ServCMDRate; }
        }
        /// <summary>
        /// C轴实际位置【已经转换】
        /// </summary>
        internal double Position_C
        {
            set
            {
                if (value != m_CmdPosition_C)
                {
                    if (!GlobalVar.Machine.Pause)//--仅在正常作业情况下检测[20180910 lqz]
                        if (!(Tag_Lock1.CurrentValue && Tag_Lock2.CurrentValue && Tag_LockBefore.CurrentValue)) StopMove(GlobalVar.AxisC.LinkIndex);//门未关好，禁止移动
                    if (!Tag_LightSensor.CurrentValue) StopMove(GlobalVar.AxisC.LinkIndex);//C轴在处理PCS异常时可以动作
                    UpdatePositionC(value / GlobalVar.MotorRate);
                }
                m_CmdPosition_C = value;
            }
            get { return (double)m_CmdPosition_C / GlobalVar.MotorRate; }
        }
        /// <summary>
        ///D轴实际位置【已经转换】
        /// </summary>
        internal double Position_D
        {
            set
            {
                if (value != m_CmdPosition_D)
                {
                    if (!GlobalVar.Machine.Pause)//--仅在正常作业情况下检测[20180910 lqz]
                        if (!(Tag_Lock1.CurrentValue && Tag_Lock2.CurrentValue && Tag_LockBefore.CurrentValue && Tag_LightSensor.CurrentValue)) StopMove(GlobalVar.AxisD.LinkIndex);//门未关好，禁止移动
                    UpdatePositionD(value / GlobalVar.MotorRate);
                }
                m_CmdPosition_D = value;
            }
            get { return (double)m_CmdPosition_D / GlobalVar.MotorRate; }
        }
        private void UpdatePositionX(double position)
        {
            if (this.Event_UpdatePositionX != null) this.Event_UpdatePositionX(position);
        }

        private void UpdatePositionY(double position)
        {
            if (this.Event_UpdatePositionY != null) this.Event_UpdatePositionY(position);
        }

        private void UpdatePositionZ(double position)
        {
            if (this.Event_UpdatePositionZ != null) this.Event_UpdatePositionZ(position);
        }
        private void UpdatePositionA(double position)
        {
            if (this.Event_UpdatePositionA != null) this.Event_UpdatePositionA(position);
        }

        private void UpdatePositionC(double position)
        {
            if (this.Event_UpdatePositionC != null) this.Event_UpdatePositionC(position);
        }

        private void UpdatePositionD(double position)
        {
            if (this.Event_UpdatePositionD != null) this.Event_UpdatePositionD(position);
        }
        private void UpdatePositionB(double position)
        {
            if (this.Event_UpdatePositionB != null) this.Event_UpdatePositionB(position);
        }


        #endregion
        #region 轴运动目标
        private double m_CmdTarget_X = 0.000d;//脉冲数--x轴
        private double m_CmdTarget_Y = 0.000d;//脉冲数--y轴
        private double m_CmdTarget_C = 0.000d;//脉冲数--IC上料轴
        private double m_CmdTarget_D = 0.000d;//脉冲数--pcs上料轴
        private double m_CmdTarget_B = 0.000d;//脉冲数--下料轴
        private double m_CmdTarget_A = 0.000d;//脉冲数--上料轴
        /// <summary>
        /// A轴的当前目标
        /// </summary>
        internal double Target_A
        {
            set
            {
                m_CmdTarget_A = value;
            }
            get { return (double)m_CmdTarget_A / GlobalVar.ServCMDRate; }
        }
        /// <summary>
        /// B轴的当前目标
        /// </summary>
        internal double Target_B
        {
            set
            {
                m_CmdTarget_B = value;
            }
            get { return (double)m_CmdTarget_B / GlobalVar.ServCMDRate; }
        }
        /// <summary>
        /// C轴的当前目标
        /// </summary>
        internal double Target_C
        {
            set
            {
                m_CmdTarget_C = value;
            }
            get { return (double)m_CmdTarget_C / GlobalVar.MotorRate; }
        }
        /// <summary>
        /// D轴的当前目标
        /// </summary>
        internal double Target_D
        {
            set
            {
                m_CmdTarget_D = value;
            }
            get { return (double)m_CmdTarget_D / GlobalVar.MotorRate; }
        }
        /// <summary>
        /// B轴的当前目标
        /// </summary>
        internal double Target_X
        {
            set
            {
                m_CmdTarget_X = value;
            }
            get { return (double)m_CmdTarget_X / GlobalVar.ServCMDRate; }
        }
        /// <summary>
        /// B轴的当前目标
        /// </summary>
        internal double Target_Y
        {
            set
            {
                m_CmdTarget_Y = value;
            }
            get { return (double)m_CmdTarget_Y / GlobalVar.ServCMDRate; }
        }
        #endregion

        /// <summary>
        /// 急停松开计时器
        /// </summary>
        private Stopwatch EMGRelease = new Stopwatch();

        private MyFunction myfunction = new MyFunction();
        Logs log = Logs.LogsT();
        public PCI1285_E()
        {
            try
            {
                int Result;
                string strTemp;
                if (GetDevCfgDllDrvVer() == false) return;//非必要步骤，获取驱动的版本
                // Get the list of available device numbers and names of devices, of which driver has been loaded successfully 
                //If you have two/more board,the device list(m_avaDevs) may be changed when the slot of the boards changed,for example:m_avaDevs[0].szDeviceName to PCI-1245
                //m_avaDevs[1].szDeviceName to PCI-1245L,changing the slot，Perhaps the opposite 
                Result = Motion.mAcm_GetAvailableDevs(CurAvaliableDevs, Motion.MAX_DEVICES, ref deviceCount);//获取所有可用的硬件设备
                if (Result != (int)ErrorCode.SUCCESS)
                {
                    //获取异常
                    strTemp = "Get Device Numbers Failed With Error Code: [0x" + Convert.ToString(Result, 16) + "]";
                    ShowMessages(strTemp, (uint)Result);
                }
                if (deviceCount > 0) DeviceNum = CurAvaliableDevs[0].DeviceNum;
                OpenBoard();
                Tag_CylinderFeed.LinkInputSignal(FeedCylinderUpper, FeedCylinderUnder);//阻挡气缸
                Tag_CylinderPCS.LinkInputSignal(PCSCylinderUpper, PCSCylinderUnder);//阻挡气缸
                Tag_CylinderDrop.LinkInputSignal(DropCylinderUpper, DropCylinderUnder);//上升气缸
                Tag_FeedLeftCheck.LinkInputSignal(FeedCylinderUpper, FeedLeftCheck);//上料轴左吸气
                Tag_FeedRightCheck.LinkInputSignal(FeedCylinderUpper, FeedRightCheck);//上料轴右吸气
                Tag_DropCheck.LinkInputSignal(DropCylinderUpper, DropCheck);//下料轴吸气
                Tag_PCSCheck1.LinkInputSignal(PCSCylinderUpper, PCSCheck1);//PCS吸取1
                Tag_PCSCheck2.LinkInputSignal(PCSCylinderUpper, PCSCheck2);//PCS吸取2
                Tag_PCSCheck3.LinkInputSignal(PCSCylinderUpper, PCSCheck3);//PCS吸取3
                Tag_PCSCheck4.LinkInputSignal(PCSCylinderUpper, PCSCheck4);//PCS吸取4
                AddAxisIntoGroup(0);
                AddAxisIntoGroup(1);
            }
            catch (Exception ex)
            {
                MsgBox(ex.Message, Color.Red, MessageBoxButtons.OK);
            }
        }


        //获取运动控制驱动版本信息
        private Boolean GetDevCfgDllDrvVer()
        {
            string fileName = "";
            FileVersionInfo myFileVersionInfo;
            string FileVersion = "";
            fileName = Environment.SystemDirectory + "\\ADVMOT.dll";//SystemDirectory指System32 
            myFileVersionInfo = FileVersionInfo.GetVersionInfo(fileName);
            FileVersion = myFileVersionInfo.FileVersion;
            string DetailMessage;
            string[] strSplit = FileVersion.Split(',');
            if (Convert.ToUInt16(strSplit[0]) < 2)
            {

                DetailMessage = "The Driver Version  Is Too Low" + "\r\nYou can update the driver through the driver installation package ";
                DetailMessage = DetailMessage + "\r\nThe Current Driver Version Number is " + FileVersion;
                DetailMessage = DetailMessage + "\r\nYou need to update the driver to 2.0.0.0 version and above";
                MessageBox.Show(DetailMessage, "DIO", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }

        private void OpenBoard()
        {
            uint Result;
            uint i = 0;
            uint AxesPerDev = new uint();
            string strTemp;
            //Open a specified device to get device handle
            //you can call GetDevNum() API to get the devcie number of fixed equipment in this,as follow
            //DeviceNum = GetDevNum((uint)DevTypeID.PCI1285, 15, 0, 0);
            Result = Motion.mAcm_DevOpen(DeviceNum, ref m_DeviceHandle);
            if (Result != (uint)ErrorCode.SUCCESS)
            {
                strTemp = "Open Device Failed With Error Code: [0x" + Convert.ToString(Result, 16) + "]";
                ShowMessages(strTemp, Result);
                return;
            }
            //FT_DevAxesCount:Get axis number of this device.
            //if you device is fixed(for example: PCI-1245),You can not get FT_DevAxesCount property value
            //This step is not necessary
            //You can also use the old API: Motion.mAcm_GetProperty(m_DeviceHandle, (uint)PropertyID.FT_DevAxesCount, ref AxesPerDev, ref BufferLength);
            // UInt32 BufferLength;
            //BufferLength =4;  buffer size for the property
            Result = Motion.mAcm_GetU32Property(m_DeviceHandle, (uint)PropertyID.FT_DevAxesCount, ref AxesPerDev);
            if (Result != (uint)ErrorCode.SUCCESS)
            {
                strTemp = "Get Axis Number Failed With Error Code: [0x" + Convert.ToString(Result, 16) + "]";
                ShowMessages(strTemp, Result);
                return;
            }
            m_ulAxisCount = AxesPerDev;
            //CmbAxes.Items.Clear();
            //if you device is fixed,for example: PCI-1245 m_ulAxisCount =4
            for (i = 0; i < m_ulAxisCount; i++)
            {
                //Open every Axis and get the each Axis Handle
                //And Initial property for each Axis 		
                //Open Axis 
                Result = Motion.mAcm_AxOpen(m_DeviceHandle, (UInt16)i, ref m_Axishand[i]);
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    strTemp = "Open Axis Failed With Error Code: [0x" + Convert.ToString(Result, 16) + "]";
                    ShowMessages(strTemp, Result);
                    return;
                }
                //CmbAxes.Items.Add(String.Format("{0:d}-Axis", i));
                //Reset Command Counter
                double cmdPosition = new double();
                cmdPosition = 0;
                //Set command position for the specified axis
                Motion.mAcm_AxSetCmdPosition(m_Axishand[i], cmdPosition);
                //Set actual position for the specified axis
                Motion.mAcm_AxSetActualPosition(m_Axishand[i], cmdPosition);
            }
            //加载配置文档，否则设备不能初始化运动
            Result = Motion.mAcm_DevLoadConfig(m_DeviceHandle, Application.StartupPath + @"\Config\PCI1285E.cfg");
            if (Result != (uint)ErrorCode.SUCCESS)
            {
                MessageBox.Show("运动控制卡加载配置文档出错，请检查！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            m_bInit = true;
            EMGRelease.Start();

            Thread m_monitorthread = new Thread(new ThreadStart(Backthread_BoardMonitorContinous))
            {
                IsBackground = true
            };//监控信号的线程
            m_monitorthread.Start();

            Thread m_monitorPositionthread = new Thread(new ThreadStart(Backthread_BoardMonitorPositionContinous))
            {
                IsBackground = true
            };//监控坐标的线程
            m_monitorPositionthread.Start();
        }
        //监控坐标 线程
        private void Backthread_BoardMonitorPositionContinous()
        {
            uint AxState_IO_X = 0;//X轴的IO信号
            uint AxState_IO_Y = 0;//Y轴的IO信号
            uint AxState_IO_A = 0;//U轴的IO信号
            uint AxState_IO_C = 0;//A轴的IO信号
            uint AxState_IO_D = 0;//B轴的IO信号
            uint AxState_IO_B = 0;//C轴的IO信号
            while (!GlobalVar.SoftWareShutDown)
            {
                try
                {
                    #region 读取状态 -- 紧急信号获取
                    //////获取X轴的当前状态
                    ////Motion.mAcm_AxGetState(m_Axishand[m_AxisNum_X], ref AxState_X);
                    //////获取Y轴的当前状态
                    ////Motion.mAcm_AxGetState(m_Axishand[m_AxisNum_Y], ref AxState_Y);

                    Motion.mAcm_AxGetMotionIO(m_Axishand[GlobalVar.AxisX.LinkIndex], ref AxState_IO_X);
                    Motion.mAcm_AxGetMotionIO(m_Axishand[GlobalVar.AxisY.LinkIndex], ref AxState_IO_Y);
                    Motion.mAcm_AxGetMotionIO(m_Axishand[GlobalVar.AxisA.LinkIndex], ref AxState_IO_A);
                    Motion.mAcm_AxGetMotionIO(m_Axishand[GlobalVar.AxisC.LinkIndex], ref AxState_IO_C);
                    Motion.mAcm_AxGetMotionIO(m_Axishand[GlobalVar.AxisD.LinkIndex], ref AxState_IO_D);
                    Motion.mAcm_AxGetMotionIO(m_Axishand[GlobalVar.AxisB.LinkIndex], ref AxState_IO_B);
                    IO_X_Alarm.CurrentValue = (AxState_IO_X & (uint)Ax_Motion_IO.AX_MOTION_IO_ALM) > 0;//X轴报警
                    IO_X_LimtP.CurrentValue = (AxState_IO_X & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTP) > 0;//X轴正限位
                    IO_X_LimtN.CurrentValue = (AxState_IO_X & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTN) > 0;//X轴正限位

                    IO_Y_Alarm.CurrentValue = (AxState_IO_Y & (uint)Ax_Motion_IO.AX_MOTION_IO_ALM) > 0;//Y轴报警
                    IO_Y_LimtP.CurrentValue = (AxState_IO_Y & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTP) > 0;//Y轴正限位
                    IO_Y_LimtN.CurrentValue = (AxState_IO_Y & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTN) > 0;//Y轴正限位

                    IO_A_Alarm.CurrentValue = (AxState_IO_A & (uint)Ax_Motion_IO.AX_MOTION_IO_ALM) > 0;//A轴报警
                    IO_A_LimtP.CurrentValue = (AxState_IO_A & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTP) > 0;//A轴正限位
                    IO_A_LimtN.CurrentValue = (AxState_IO_A & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTN) > 0;//A轴正限位

                    IO_C_Alarm.CurrentValue = (AxState_IO_C & (uint)Ax_Motion_IO.AX_MOTION_IO_ALM) > 0;//C轴报警
                    IO_C_LimtP.CurrentValue = (AxState_IO_C & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTP) > 0;//C轴正限位
                    IO_C_LimtN.CurrentValue = (AxState_IO_C & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTN) > 0;//C轴正限位

                    IO_D_Alarm.CurrentValue = (AxState_IO_D & (uint)Ax_Motion_IO.AX_MOTION_IO_ALM) > 0;//D轴报警
                    IO_D_LimtP.CurrentValue = (AxState_IO_D & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTP) > 0;//D轴正限位
                    IO_D_LimtN.CurrentValue = (AxState_IO_D & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTN) > 0;//D轴正限位

                    IO_B_Alarm.CurrentValue = (AxState_IO_B & (uint)Ax_Motion_IO.AX_MOTION_IO_ALM) > 0;//C轴报警
                    IO_B_LimtP.CurrentValue = (AxState_IO_B & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTP) > 0;//C轴正限位
                    IO_B_LimtN.CurrentValue = (AxState_IO_B & (uint)Ax_Motion_IO.AX_MOTION_IO_LMTN) > 0;//C轴正限位


                    //紧急状态
                    byte BitIn_EMG = 0;
                    Motion.mAcm_AxDiGetBit(m_Axishand[EMGSTOP.AxisNum], EMGSTOP.Channel, ref BitIn_EMG);
                    if (BitIn_EMG != 1)
                    {
                        if (!GlobalVar.EMGSTOP)
                        {
                            GlobalVar.EMGSTOP = true;
                            if (Event_EMGStop != null) Event_EMGStop();//紧急停止
                        }
                    }
                    //紧急停止解除
                    if (GlobalVar.EMGSTOP)
                    {
                        if (BitIn_EMG == 1)
                        {
                            GlobalVar.EMGSTOP = false;
                            EMGRelease.Restart();
                            //读取之前清除一下异常信号
                            ResetAlarmError();
                            if (Event_EMGStop != null) Event_EMGStop();//解除紧急停止
                        }
                    }
                    #endregion

                    #region  启动按键按下 信号
                    byte BitIn_Start = 0;
                    UInt32 Result_Start = Motion.mAcm_AxDiGetBit(m_Axishand[Start.AxisNum], Start.Channel, ref BitIn_Start);
                    if (Result_Start == (uint)ErrorCode.SUCCESS)
                    {
                        Tag_SetStart.CurrentValue = BitIn_Start == 1;
                    }
                    #endregion

                    #region  复位 信号
                    byte BitIn_Reset = 0;
                    UInt32 Result_Reset = Motion.mAcm_AxDiGetBit(m_Axishand[Reset.AxisNum], Reset.Channel, ref BitIn_Reset);
                    if (Result_Reset == (uint)ErrorCode.SUCCESS)
                    {
                        Tag_Reset.CurrentValue = BitIn_Reset == 1;
                    }
                    #endregion

                    #region 蜂鸣器信号
                    byte BitDo_Buzzer = 0;
                    UInt32 Result_Buzzer = Motion.mAcm_AxDoGetBit(m_Axishand[AlarmLight_Buzzer.AxisNum], AlarmLight_Buzzer.Channel, ref BitDo_Buzzer);
                    if (Result_Buzzer == (uint)ErrorCode.SUCCESS)
                    {
                        Tag_Buzzer.CurrentValue = BitDo_Buzzer == 1;
                    }
                    #endregion

                    #region 光栅
                    byte BitIn_LightSensor = 0;
                    UInt32 Result_LightSensor = Motion.mAcm_AxDiGetBit(m_Axishand[LightSensor.AxisNum], LightSensor.Channel, ref BitIn_LightSensor);
                    if (Result_LightSensor == (uint)ErrorCode.SUCCESS)
                        Tag_LightSensor.CurrentValue = BitIn_LightSensor == 1;
                    #endregion

                    #region 安全锁信号
                    byte BitIn_Front1 = 0;
                    UInt32 Result_Front1 = Motion.mAcm_AxDiGetBit(m_Axishand[LockIn1.AxisNum], LockIn1.Channel, ref BitIn_Front1);
                    if (Result_Front1 == (uint)ErrorCode.SUCCESS)
                    {
                        Tag_Lock1.CurrentValue = BitIn_Front1 == 1;
                    }
                    byte BitIn_Front2 = 0;
                    UInt32 Result_Front2 = Motion.mAcm_AxDiGetBit(m_Axishand[LockIn2.AxisNum], LockIn2.Channel, ref BitIn_Front2);
                    if (Result_Front2 == (uint)ErrorCode.SUCCESS)
                    {
                        Tag_Lock2.CurrentValue = BitIn_Front2 == 1;
                    }
                    byte BitIn_Back = 0;
                    UInt32 Result_Back = Motion.mAcm_AxDiGetBit(m_Axishand[LockInBefore.AxisNum], LockInBefore.Channel, ref BitIn_Back);
                    if (Result_Back == (uint)ErrorCode.SUCCESS)
                    {
                        Tag_LockBefore.CurrentValue = BitIn_Back == 1;
                    }
                    #endregion

                    #region 气缸信号
                    byte BitIn_CylinderUP = 0;
                    UInt32 Result_CylinderUP = Motion.mAcm_AxDoGetBit(m_Axishand[CylinderFeed.AxisNum], CylinderFeed.Channel, ref BitIn_CylinderUP);
                    if (Result_CylinderUP == (uint)ErrorCode.SUCCESS)
                    {
                        Tag_CylinderFeed.CurrentValue = BitIn_CylinderUP == 1;
                    }
                    byte BitIn_CylinderOut = 0;
                    UInt32 Result_CylinderOut = Motion.mAcm_AxDoGetBit(m_Axishand[CylinderDrop.AxisNum], CylinderDrop.Channel, ref BitIn_CylinderOut);
                    if (Result_CylinderOut == (uint)ErrorCode.SUCCESS)
                    {
                        Tag_CylinderDrop.CurrentValue = BitIn_CylinderOut == 1;
                    }
                    byte BitIn_CylinderPCS = 0;
                    UInt32 Result_CylinderPCS = Motion.mAcm_AxDoGetBit(m_Axishand[CylinderPCS.AxisNum], CylinderPCS.Channel, ref BitIn_CylinderPCS);
                    if (Result_CylinderPCS == (uint)ErrorCode.SUCCESS)
                    {
                        Tag_CylinderPCS.CurrentValue = BitIn_CylinderPCS == 1;
                    }




                    #endregion

                    #region 真空
                    byte BitIn_CheckLeft = 0;
                    UInt32 Result_CheckLeft = Motion.mAcm_AxDoGetBit(m_Axishand[CylinderLeftUpper.AxisNum], CylinderLeftUpper.Channel, ref BitIn_CheckLeft);
                    if (Result_CheckLeft == (uint)ErrorCode.SUCCESS)
                    {
                        Tag_FeedLeftCheck.CurrentValue = BitIn_CheckLeft == 1;
                    }
                    byte BitIn_CheckRight = 0;
                    UInt32 Result_CheckRight = Motion.mAcm_AxDoGetBit(m_Axishand[CylinderRightUpper.AxisNum], CylinderRightUpper.Channel, ref BitIn_CheckRight);
                    if (Result_CheckRight == (uint)ErrorCode.SUCCESS)
                    {
                        Tag_FeedRightCheck.CurrentValue = BitIn_CheckRight == 1;
                    }

                    byte BitIn_CheckDown = 0;
                    UInt32 Result_CheckDown = Motion.mAcm_AxDoGetBit(m_Axishand[CylinderDropUpper.AxisNum], CylinderDropUpper.Channel, ref BitIn_CheckDown);
                    if (Result_CheckDown == (uint)ErrorCode.SUCCESS)
                    {
                        Tag_DropCheck.CurrentValue = BitIn_CheckDown == 1;
                    }
                    byte BitIn_CheckPCS1 = 0;
                    UInt32 Result_CheckPCS1 = Motion.mAcm_AxDoGetBit(m_Axishand[CylinderPCSUpper.AxisNum], CylinderPCSUpper.Channel, ref BitIn_CheckPCS1);
                    if (Result_CheckPCS1 == (uint)ErrorCode.SUCCESS)
                    {
                        Tag_PCSCheck1.CurrentValue = BitIn_CheckPCS1 == 1;
                    }
                    byte BitIn_CheckPCS2 = 0;
                    UInt32 Result_CheckPCS2 = Motion.mAcm_AxDoGetBit(m_Axishand[CylinderPCSUpper.AxisNum], CylinderPCSUpper.Channel, ref BitIn_CheckPCS2);
                    if (Result_CheckPCS2 == (uint)ErrorCode.SUCCESS)
                    {
                        Tag_PCSCheck2.CurrentValue = BitIn_CheckPCS2 == 1;
                    }
                    byte BitIn_CheckPCS3 = 0;
                    UInt32 Result_CheckPCS3 = Motion.mAcm_AxDoGetBit(m_Axishand[CylinderPCSUpper.AxisNum], CylinderPCSUpper.Channel, ref BitIn_CheckPCS3);
                    if (Result_CheckPCS3 == (uint)ErrorCode.SUCCESS)
                    {
                        Tag_PCSCheck3.CurrentValue = BitIn_CheckPCS3 == 1;
                    }
                    byte BitIn_CheckPCS4 = 0;
                    UInt32 Result_CheckPCS4 = Motion.mAcm_AxDoGetBit(m_Axishand[CylinderPCSUpper.AxisNum], CylinderPCSUpper.Channel, ref BitIn_CheckPCS4);
                    if (Result_CheckPCS4 == (uint)ErrorCode.SUCCESS)
                    {
                        Tag_PCSCheck4.CurrentValue = BitIn_CheckPCS4 == 1;
                    }
                    #endregion
                }
                catch (Exception ex)
                {
                    log.AddERRORLOG("板卡后台监测线程异常！\r\n" + ex.ToString());
                }
                Thread.Sleep(100);
            }
        }
        //监控信号 线程
        private void Backthread_BoardMonitorContinous()
        {
            while (!GlobalVar.SoftWareShutDown)
            {
                try
                {
                    if (m_bInit)
                    {
                        double CurPos = new double();

                        //获取X轴的当前实际位置
                        Motion.mAcm_AxGetCmdPosition(m_Axishand[GlobalVar.AxisX.LinkIndex], ref CurPos);
                        Position_X = CurPos;
                        //获取Y轴的当前实际位置
                        Motion.mAcm_AxGetCmdPosition(m_Axishand[GlobalVar.AxisY.LinkIndex], ref CurPos);
                        Position_Y = CurPos;
                        //获取Z轴的当前实际位置
                        Motion.mAcm_AxGetCmdPosition(m_Axishand[GlobalVar.AxisZ.LinkIndex], ref CurPos);
                        Position_Z = CurPos;
                        //获取a轴的当前实际位置
                        Motion.mAcm_AxGetCmdPosition(m_Axishand[GlobalVar.AxisA.LinkIndex], ref CurPos);
                        Position_A = CurPos;
                        //获取C轴的当前实际位置
                        Motion.mAcm_AxGetCmdPosition(m_Axishand[GlobalVar.AxisC.LinkIndex], ref CurPos);
                        Position_C = CurPos;
                        //获取D轴的当前实际位置
                        Motion.mAcm_AxGetCmdPosition(m_Axishand[GlobalVar.AxisD.LinkIndex], ref CurPos);
                        Position_D = CurPos;
                        //获取B轴的当前实际位置
                        Motion.mAcm_AxGetCmdPosition(m_Axishand[GlobalVar.AxisB.LinkIndex], ref CurPos);
                        Position_B = CurPos;
                    }
                }
                catch (Exception ex)
                {
                    log.AddERRORLOG("板卡后台监测线程异常！\r\n" + ex.ToString());
                }
                Thread.Sleep(100);
            }
        }


        /// <summary>
        /// 获得轴的命令脉冲
        /// </summary>
        /// <param name="Index"></param>
        /// <returns></returns>
        internal double GetCMDPosition(int Index)
        {
            double CurPos = new double();

            //获取 轴的当前实际位置
            if (Motion.mAcm_AxGetCmdPosition(m_Axishand[Index], ref CurPos) == 0)
            {
                return CurPos;
            }
            else return 0;
        }

        /// <summary>
        /// 设置轴的当前脉冲
        /// </summary>
        /// <param name="AxisIndex"></param>
        /// <param name="cmdPosition"></param>
        internal void SetCMDPosition(int AxisIndex, double cmdPosition = 0)
        {
            //Set command position for the specified axis
            uint Result = Motion.mAcm_AxSetCmdPosition(m_Axishand[AxisIndex], cmdPosition);
            if (Result != (uint)ErrorCode.SUCCESS)
            {
                MessageBox.Show(string.Format("设置轴{0} 脉冲错误，请检查！", AxisIndex), "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        //软件重置异常
        internal void ResetAlarmError()
        {
            UInt16 State = new UInt16();
            if (m_bInit == true)
            {
                Motion.mAcm_AxResetError(m_Axishand[0]);
                Motion.mAcm_AxResetError(m_Axishand[1]);
                Motion.mAcm_GpGetState(m_GpHand, ref State);
                if (State == (UInt16)GroupState.STA_Gp_ErrorStop)
                {
                    //当群组处于发生错误停止时，复位错误
                    Motion.mAcm_GpResetError(m_GpHand);
                }
            }
        }

        /// <summary>
        /// 设置轴的伺服开启或者关闭
        /// </summary>
        /// <param name="Axis">伺服</param>
        /// <param name="SetOn">开启或者关闭</param>
        internal void ServerOn(AxisProperty Axis, bool SetOn)
        {
            int AxisNum = Axis.LinkIndex;
            UInt32 Result;
            string strTemp;
            //Check the servoOno flag to decide if turn on or turn off the ServoOn output.
            if (m_bInit != true)
            {
                return;
            }
            if (!Axis.ServerOn)
            {
                // Set servo Driver ON,1: On
                Result = Motion.mAcm_AxSetSvOn(m_Axishand[AxisNum], 1);
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    strTemp = "伺服开启异常,异常代码: [0x" + Convert.ToString(Result, 16) + "]";
                    ShowMessages(strTemp, Result);
                    return;
                }
                Axis.ServerOn = true;
            }
            else
            {
                // Set servo Driver OFF,0: Off
                Result = Motion.mAcm_AxSetSvOn(m_Axishand[AxisNum], 0);
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    strTemp = "伺服关闭异常,异常代码: [0x" + Convert.ToString(Result, 16) + "]";
                    ShowMessages(strTemp, Result);
                    return;
                }
                Axis.ServerOn = false;
            }
        }

        /// <summary>
        /// 获取轴速度参数
        /// </summary>
        /// <param name="Index">轴编号</param>
        /// <param name="VelLow">最低速度</param>
        /// <param name="VelHigh">最高速度</param>
        /// <param name="VelAcc">加速度</param>
        /// <param name="VelDec">减速度</param>
        /// <param name="Jerk">速度曲线</param>
        /// <returns></returns>
        internal bool GetAxisVelParam(int Index, ref double VelLow, ref double VelHigh, ref double VelAcc, ref double VelDec, ref double Jerk)
        {
            UInt32 Result;
            string strTemp = "";
            //Get low velocity (start velocity) of this axis (Unit: PPU/S).
            //You can also use the old API:  Acm_GetProperty(m_Axishand[CmbAxes.SelectedIndex], (uint)PropertyID.PAR_AxVelLow, ref axvellow,ref BufferLength);
            // uint BufferLength;
            // BufferLength = 8; buffer size for the property
            Result = Motion.mAcm_GetF64Property(m_Axishand[Index], (uint)PropertyID.PAR_AxVelLow, ref VelLow);
            if (Result != (uint)ErrorCode.SUCCESS)
            {
                strTemp = "Get low velocity failed with error code: [0x" + Convert.ToString(Result, 16) + "]";
                ShowMessages(strTemp, Result);
                return false;
            }
            //get high velocity (driving velocity) of this axis (Unit: PPU/s).
            //You can also use the old API:  Acm_GetProperty(m_Axishand[CmbAxes.SelectedIndex], (uint)PropertyID.PAR_AxVelHigh, ref axvelhigh,ref BufferLength);
            // uint BufferLength;
            // BufferLength = 8; buffer size for the property
            Result = Motion.mAcm_GetF64Property(m_Axishand[Index], (uint)PropertyID.PAR_AxVelHigh, ref VelHigh);
            if (Result != (uint)ErrorCode.SUCCESS)
            {
                strTemp = "Get High velocity failed with error code: [0x" + Convert.ToString(Result, 16) + "]";
                ShowMessages(strTemp, Result);
                return false;
            }
            //get acceleration of this axis (Unit: PPU/s2).
            //You can also use the old API:  Acm_GetProperty(m_Axishand[CmbAxes.SelectedIndex], (uint)PropertyID.PAR_AxAcc, ref axacc,ref BufferLength);
            // uint BufferLength;
            // BufferLength = 8; buffer size for the property
            Result = Motion.mAcm_GetF64Property(m_Axishand[Index], (uint)PropertyID.PAR_AxAcc, ref VelAcc);
            if (Result != (uint)ErrorCode.SUCCESS)
            {
                strTemp = "Get acceleration  failed with error code: [0x" + Convert.ToString(Result, 16) + "]";
                ShowMessages(strTemp, Result);
                return false;
            }
            //get deceleration of this axis (Unit: PPU/s2).
            //You can also use the old API: Motion.mAcm_GetProperty(m_Axishand[CmbAxes.SelectedIndex], (uint)PropertyID.PAR_AxDec, ref axdec, ref BufferLength);
            // uint BufferLength;
            // BufferLength = 8; buffer size for the property
            Result = Motion.mAcm_GetF64Property(m_Axishand[Index], (uint)PropertyID.PAR_AxDec, ref VelDec);
            if (Result != (uint)ErrorCode.SUCCESS)
            {
                strTemp = "Get deceleration  failed with error code: [0x" + Convert.ToString(Result, 16) + "]";
                ShowMessages(strTemp, Result);
                return false;
            }

            Result = Motion.mAcm_GetF64Property(m_Axishand[Index], (uint)PropertyID.PAR_AxJerk, ref Jerk);
            if (Result != (uint)ErrorCode.SUCCESS)
            {
                strTemp = "Get deceleration  failed with error code: [0x" + Convert.ToString(Result, 16) + "]";
                ShowMessages(strTemp, Result);
                return false;
            }
            return true;
        }


        #region 轴速度获取 
        //单轴最高速度获取-- type{true:群组，false:单轴}
        public uint GetProp_VelHigh(uint AxisNum, ref double VelHigh)
        {
            uint buffLen = 4;
            uint result = Motion.mAcm_GetProperty(m_Axishand[AxisNum],
                (uint)PropertyID.PAR_AxVelHigh,
                ref VelHigh, ref buffLen);
            return result;
        }
        //单轴最低速度获取-- type{true:群组，false:单轴}
        public uint GetProp_VelLow(uint AxisNum, ref double VelLow)
        {
            uint buffLen = 4;
            uint result = Motion.mAcm_GetProperty(m_Axishand[AxisNum],
                (uint)PropertyID.PAR_AxVelLow,
                ref VelLow, ref buffLen);
            return result;
        }
        //单轴加速度获取-- type{true:群组，false:单轴}
        public uint GetProp_Acc(uint AxisNum, ref double VelAcc)
        {
            uint buffLen = 4;
            uint result = Motion.mAcm_GetProperty(m_Axishand[AxisNum],
                (uint)PropertyID.PAR_AxAcc,
                ref VelAcc, ref buffLen);
            return result;
        }
        //单轴减速度获取-- type{true:群组，false:单轴}
        public uint GetProp_Dec(uint AxisNum, ref double VelDec)
        {
            uint buffLen = 4;
            uint result = Motion.mAcm_GetProperty(m_Axishand[AxisNum],
                (uint)PropertyID.PAR_AxDec,
                ref VelDec, ref buffLen);
            return result;
        }
        //单轴速度曲线获取-- type{true:群组，false:单轴}
        public uint GetProp_Jerk(uint AxisNum, ref double Jerk)
        {
            uint buffLen = 4;
            uint result = Motion.mAcm_GetProperty(m_Axishand[AxisNum],
                (uint)PropertyID.PAR_AxJerk,
                ref Jerk, ref buffLen);
            return result;
        }
        #endregion
        /// <summary>
        /// 获取轴状态
        /// </summary>
        /// <param name="Index"></param>
        internal AxisState GetAxisState(int Index)
        {
            UInt16 AxState = new UInt16();
            //Get the Axis's current state
            Motion.mAcm_AxGetState(m_Axishand[Index], ref AxState);
            return (AxisState)AxState;
        }



        //获取轴的IO信号
        internal bool GetIOState(int Index, ref UInt32 IOStatus)
        {
            UInt32 Result;
            if (m_bInit)
            {
                //Get the motion I/O status of the axis.
                Result = Motion.mAcm_AxGetMotionIO(m_Axishand[Index], ref IOStatus);
                return (Result == (uint)ErrorCode.SUCCESS);
            }
            else return false;
        }

        //获取轴的输入信号
        internal bool GetDI(int Index, ref bool DI0, ref bool DI1, ref bool DI2, ref bool DI3)
        {
            bool getresult = true;
            UInt32 Result;
            byte BitIn = 0;
            for (ushort i = 0; i < 4; i++)
            {
                //Get the specified channel's DI value
                Result = Motion.mAcm_AxDiGetBit(m_Axishand[Index], i, ref BitIn);
                bool di = (BitIn == 1);
                switch (i)
                {
                    case 0:
                        DI0 = di;
                        break;
                    case 1:
                        DI1 = di;
                        break;
                    case 2:
                        DI2 = di;
                        break;
                    case 3:
                        DI3 = di;
                        break;
                    default:
                        return false;
                }
                getresult &= (Result == (uint)ErrorCode.SUCCESS);
            }
            return getresult;
        }

        //获取单个轴的输入信号
        internal bool GetSingleDI(BoardSignalDefinition Signal, ref bool signal)
        {
            UInt32 Result;
            if (m_bInit != true)
            {
                throw new Exception("板卡未初始化！");
            }
            byte bitDo = 0;
            //Get the specified channel's DO value
            Result = Motion.mAcm_AxDiGetBit(m_Axishand[Signal.AxisNum], Signal.Channel, ref bitDo);
            signal = (bitDo == 1);
            return (Result == (uint)ErrorCode.SUCCESS);
        }

        //获取轴的输出信号
        internal bool GetAxisDO(int Index, ref bool D00, ref bool D01, ref bool D02, ref bool D03)
        {
            bool getresult = true;
            UInt32 Result;
            byte bitDo = 0;
            for (ushort i = 4; i < 8; i++)
            {
                //Get the specified channel's DO value
                Result = Motion.mAcm_AxDoGetBit(m_Axishand[Index], i, ref bitDo);
                bool di = (bitDo == 1);
                switch (i)
                {
                    case 4:
                        D00 = di;
                        break;
                    case 5:
                        D01 = di;
                        break;
                    case 6:
                        D02 = di;
                        break;
                    case 7:
                        D03 = di;
                        break;
                    default:
                        return false;
                }
                getresult &= (Result == (uint)ErrorCode.SUCCESS);
            }
            return getresult;
        }

        //获取单个轴的输出信号
        internal bool GetSingleDO(BoardSignalDefinition Signal, ref bool signal)
        {
            UInt32 Result;
            if (m_bInit != true)
            {
                throw new Exception("板卡未初始化！");
            }
            byte bitDo = 0;
            //Get the specified channel's DO value
            Result = Motion.mAcm_AxDoGetBit(m_Axishand[Signal.AxisNum], Signal.Channel, ref bitDo);
            signal = (bitDo == 1);
            return (Result == (uint)ErrorCode.SUCCESS);
        }

        //设置板卡的输出信号
        internal void SetDO(BoardSignalDefinition df, bool On)
        {
            UInt32 Result;
            if (m_bInit != true)
            {
                return;
            }
            //Set DO value to channel
            Result = Motion.mAcm_AxDoSetBit(m_Axishand[df.AxisNum], df.Channel, On ? (byte)1 : (byte)0);
        }

        //设置轴的输出信号
        internal void SetDO(int Index, ushort DOChannel, bool On)
        {
            UInt32 Result;
            if (m_bInit != true)
            {
                return;
            }
            //Set DO value to channel
            Result = Motion.mAcm_AxDoSetBit(m_Axishand[Index], DOChannel, On ? (byte)1 : (byte)0);
        }

        //User-defined API to close board
        internal void CloseBoard()
        {
            UInt16[] usAxisState = new UInt16[32];
            uint AxisNum;
            //Stop Every Axes
            if (m_bInit == true)
            {
                for (AxisNum = 0; AxisNum < m_ulAxisCount; AxisNum++)
                {
                    //Get the axis's current state
                    Motion.mAcm_AxGetState(m_Axishand[AxisNum], ref usAxisState[AxisNum]);
                    if (usAxisState[AxisNum] == (uint)AxisState.STA_AX_ERROR_STOP)
                    {
                        // Reset the axis' state. If the axis is in ErrorStop state, the state will be changed to Ready after calling this function
                        Motion.mAcm_AxResetError(m_Axishand[AxisNum]);
                    }
                    //To command axis to decelerate to stop.
                    Motion.mAcm_AxStopDec(m_Axishand[AxisNum]);
                }
                //移除群组中的所有轴并关闭群组句柄
                Motion.mAcm_GpClose(ref m_GpHand);
                m_GpHand = IntPtr.Zero;

                for (AxisNum = 0; AxisNum < m_ulAxisCount; AxisNum++)
                {
                    //Close Axes
                    Motion.mAcm_AxClose(ref m_Axishand[AxisNum]);
                }
                m_ulAxisCount = 0;
                //Close Device
                Motion.mAcm_DevClose(ref m_DeviceHandle);
                m_DeviceHandle = IntPtr.Zero;
                m_bInit = false;
            }
        }

        #region 清除错误
        /// <summary>
        /// 清除轴错误
        /// </summary>
        /// <param name="Index"></param>
        internal void ClearAxisError(int Index)
        {
            UInt32 Result;
            string strTemp;
            ////Reset the axis' state. If the axis is in ErrorStop state, the state will
            //be changed to Ready after calling this function.
            Result = Motion.mAcm_AxResetError(m_Axishand[Index]);
            if (Result != (uint)ErrorCode.SUCCESS)
            {
                strTemp = "Reset axis's error failed With Error Code: [0x" + Convert.ToString(Result, 16) + "]";
                ShowMessages(strTemp, Result);
                return;
            }
        }
        #endregion

        /// <summary>
        /// 设置群组速度
        /// </summary>
        /// <param name="VelHigh">最高速度</param>
        /// <param name="VelLow">初速度</param>
        /// <param name="Acc">加速度</param>
        /// <param name="Dec">减速度</param>
        public void SetProp_GPSpeed(double VelHigh, double VelLow, double Acc, double Dec)
        {
            uint result = SetProp_VelHigh(0, VelHigh, true);
            result += SetProp_VelLow(0, VelLow, true);
            result += SetProp_Jerk(0, (double)1, true);  //0：T型；1：S型
            result += SetProp_Acc(0, Acc, true);
            result += SetProp_Dec(0, Dec, true);
            if (result != 0)
            {
                log.AddERRORLOG("群组速度参数写入失败: \r\n");
            }
        }

        //添加轴至群组
        public void AddAxisIntoGroup(int AxisNum)
        {
            uint AxesInfoInGp = new uint();
            if (m_bInit != true) { return; }
            uint Result = Motion.mAcm_GpAddAxis(ref m_GpHand, m_Axishand[AxisNum]);
            if (Result != (uint)ErrorCode.SUCCESS)
            {
                MessageBox.Show("添加轴至群组失败！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            else
            {
                //添加成功
                AxCountInGp++;
                //listBoxAxesInGp.Items.Add(AxisNum == 0 ? m_AxisName_X : m_AxisName_Y);
                uint buffLen = 4;
                Result = Motion.mAcm_GetProperty(m_GpHand, (uint)PropertyID.CFG_GpAxesInGroup, ref AxesInfoInGp, ref buffLen);
                if (Result == (uint)ErrorCode.SUCCESS)
                {
                    for (int i = 0; i < 32; i++)
                    {
                        if ((AxesInfoInGp & (0x1 << i)) > 0)
                        {
                            //textBoxMasterID.Text = String.Format("{0:d}", i);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 更新消息的事件
        /// </summary>
        /// <param name="str"></param>
        private void eve_MotionMsg(string str, bool iserr = false)
        {
            if (this.Event_MotionMsg != null) this.Event_MotionMsg(str, iserr);
        }


        /// <summary>
        /// 显示错误信息
        /// </summary>
        /// <param name="DetailMessage">错误信息【错误代码】</param>
        /// <param name="errorCode">错误代码</param>
        private void ShowMessages(string DetailMessage, uint errorCode)
        {
            StringBuilder ErrorMsg = new StringBuilder("", 100);
            //Get the error message according to error code returned from API
            Boolean res = Motion.mAcm_GetErrorMessage(errorCode, ErrorMsg, 100);
            string ErrorMessage = "";
            if (res)
                ErrorMessage = ErrorMsg.ToString();
            MsgBox(DetailMessage + "\r\nError Message:" + ErrorMessage, Color.Red, MessageBoxButtons.OK);
        }
        /// <summary>
        /// 弹框【OK或者Cancel】
        /// </summary>
        /// <param name="text">内容</param>
        /// <param name="backcolor">背景色</param>
        /// <returns></returns>
        private bool MsgBox(string text, Color backcolor, MessageBoxButtons btn)
        {
            using (MsgBox box = new MsgBox(btn))
            {
                box.Title = "轴异常";
                box.ShowText = text;
                box.SetBackColor = backcolor;
                if (box.ShowDialog() == DialogResult.OK) return true;
                else return false;
            }
        }
    }
}
