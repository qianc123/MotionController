﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Advantech.Motion;

namespace OQC_IC_CHECK_System
{
    partial class PCI1285_E
    {
        public delegate void dele_MotionMsg(string str, bool iserr);
        public event dele_MotionMsg Event_MotionMsg;

        /**************轴运动前，确保门已经关闭****************/

        #region 回原点
        /// <summary>
        /// 归原点
        /// </summary>
        /// <param name="AxisNum">轴序号</param>
        /// <returns></returns>
        internal bool Home(int AxisNum)
        {
            try
            {
                UInt32 PropertyVal = new UInt32();
                double CrossDistance = new double();
                if (!m_bInit) { return false; }
                double Vel = 10000; //m_GPValue_VelLow_low; //1000.0;
                if (AxisNum == GlobalVar.AxisC.LinkIndex || AxisNum == GlobalVar.AxisD.LinkIndex) Vel = 10000;
                UInt32 Result = Motion.mAcm_SetProperty(m_Axishand[AxisNum], (uint)PropertyID.PAR_AxVelLow, ref Vel, (uint)Marshal.SizeOf(typeof(double)));
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    //MessageBox.Show("Set Property-PAR_AxVelLow Failed With Error Code[0x" + Convert.ToString(Result, 16) + "]", "Home", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                Vel = GlobalVar.HomeSpeed; //m_GPValue_VelHigh_low; //3000.0;
                if (AxisNum == GlobalVar.AxisC.LinkIndex || AxisNum == GlobalVar.AxisD.LinkIndex) Vel = 80000;
                Result = Motion.mAcm_SetProperty(m_Axishand[AxisNum], (uint)PropertyID.PAR_AxVelHigh, ref Vel, (uint)Marshal.SizeOf(typeof(double)));
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    //MessageBox.Show("Set Property-PAR_AxVelHigh Failed With Error Code[0x" + Convert.ToString(Result, 16) + "]", "Home", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                Vel = GlobalVar.HomeSpeed * 2;
                if (AxisNum == GlobalVar.AxisC.LinkIndex || AxisNum == GlobalVar.AxisD.LinkIndex) Vel = 80000;
                Result = Motion.mAcm_SetProperty(m_Axishand[AxisNum], (uint)PropertyID.PAR_AxHomeAcc, ref Vel, (uint)Marshal.SizeOf(typeof(double)));
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    return false;
                }

                Vel = GlobalVar.HomeSpeed * 2;
                if (AxisNum == GlobalVar.AxisC.LinkIndex || AxisNum == GlobalVar.AxisD.LinkIndex) Vel = 80000;
                Result = Motion.mAcm_SetProperty(m_Axishand[AxisNum], (uint)PropertyID.PAR_AxHomeDec, ref Vel, (uint)Marshal.SizeOf(typeof(double)));
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    return false;
                }

                PropertyVal = 2; //Edge on
                Result = Motion.mAcm_SetProperty(m_Axishand[AxisNum], (uint)PropertyID.PAR_AxHomeExSwitchMode, ref PropertyVal, (uint)Marshal.SizeOf(typeof(UInt32)));
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    //MessageBox.Show("Set Property-PAR_AxHomeExSwitchMode Failed With Error Code[0x" + Convert.ToString(Result, 16) + "]", "Home", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                CrossDistance = 100; //找到信号后，返回距离，初始值100
                Result = Motion.mAcm_SetProperty(m_Axishand[AxisNum], (uint)PropertyID.PAR_AxHomeCrossDistance, ref CrossDistance, (uint)Marshal.SizeOf(typeof(double)));
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    //MessageBox.Show("Set Property-AxHomeCrossDistance Failed With Error Code[0x" + Convert.ToString(Result, 16) + "]", "Home", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                //Result = Motion.mAcm_AxHome(m_Axishand[AxisNum], (UInt32)comboBoxMode.SelectedIndex, (UInt32)comboBoxDir.SelectedIndex);
                Result = Motion.mAcm_AxHome(m_Axishand[AxisNum], 11, 1);  //MODE12_AbsSearchReFind  |  Nagative Direction
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    //MessageBox.Show("AxHome Fai led With Error Code[0x" + Convert.ToString(Result, 16) + "]", "Home", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                //更新轴运动目标
                switch (AxisNum)
                {
                    case 0://axis X
                        Target_X = 0;
                        break;
                    case 1://axis Y
                        Target_Y = 0;
                        break;
                    case 4://axis A
                        Target_A = 0;
                        break;
                    case 5://axis B
                        Target_B = 0;
                        break;
                    case 6://axis C
                        Target_C = 0;
                        break;
                    case 7://axis D
                        Target_D = 0;
                        break;
                }
            }
            catch
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 回原点
        /// </summary>
        /// <param name="AxisNum">轴</param>
        /// <param name="dir">方向 0：正向； 1：负向</param>
        /// <returns></returns>
        internal bool Home(int AxisNum, uint dir)
        {
            try
            {
                UInt32 PropertyVal = new UInt32();
                double CrossDistance = new double();
                if (!m_bInit) { return false; }
                double Vel = GlobalVar.HomeSpeed; //m_GPValue_VelLow_low; //1000.0;
                UInt32 Result = Motion.mAcm_SetProperty(m_Axishand[AxisNum], (uint)PropertyID.PAR_AxVelLow, ref Vel, (uint)Marshal.SizeOf(typeof(double)));
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    //MessageBox.Show("Set Property-PAR_AxVelLow Failed With Error Code[0x" + Convert.ToString(Result, 16) + "]", "Home", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                Vel = GlobalVar.HomeSpeed; //m_GPValue_VelHigh_low; //3000.0;
                Result = Motion.mAcm_SetProperty(m_Axishand[AxisNum], (uint)PropertyID.PAR_AxVelHigh, ref Vel, (uint)Marshal.SizeOf(typeof(double)));
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    //MessageBox.Show("Set Property-PAR_AxVelHigh Failed With Error Code[0x" + Convert.ToString(Result, 16) + "]", "Home", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                PropertyVal = 2; //Edge on
                Result = Motion.mAcm_SetProperty(m_Axishand[AxisNum], (uint)PropertyID.PAR_AxHomeExSwitchMode, ref PropertyVal, (uint)Marshal.SizeOf(typeof(UInt32)));
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    //MessageBox.Show("Set Property-PAR_AxHomeExSwitchMode Failed With Error Code[0x" + Convert.ToString(Result, 16) + "]", "Home", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                CrossDistance = 1500; //找到信号后，返回距离，初始值100
                Result = Motion.mAcm_SetProperty(m_Axishand[AxisNum], (uint)PropertyID.PAR_AxHomeCrossDistance, ref CrossDistance, (uint)Marshal.SizeOf(typeof(double)));
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    //MessageBox.Show("Set Property-AxHomeCrossDistance Failed With Error Code[0x" + Convert.ToString(Result, 16) + "]", "Home", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                //Result = Motion.mAcm_AxHome(m_Axishand[AxisNum], (UInt32)comboBoxMode.SelectedIndex, (UInt32)comboBoxDir.SelectedIndex);
                Result = Motion.mAcm_AxHome(m_Axishand[AxisNum], 11, dir);  //MODE12_AbsSearchReFind  |  Nagative Direction
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    //MessageBox.Show("AxHome Failed With Error Code[0x" + Convert.ToString(Result, 16) + "]", "Home", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            catch
            {
                return false;
            }
            return true;
        }
        #endregion

        #region PTP
        /// <summary>
        /// 点对点运动
        /// </summary>
        /// <param name="AxisIndex">轴编号</param>
        /// <param name="DIR">是否正向运动</param>
        /// <param name="movevalue">运动距离，单位：脉冲</param>
        /// <param name="Rel">是否相对运动</param>
        internal void MoveDIR(int AxisIndex, bool DIR, double movevalue, bool Rel)
        {
            if (EMGRelease.Elapsed.TotalSeconds < 2) return;//急停松开时间小于两秒，忽略运动
            if (!MoveEnbale) throw new Exception("未完成复位，禁止运动！");
            if (!DIR) movevalue *= -1;

            UInt32 Result;
            string strTemp;
            if (m_bInit)
            {
                if (Rel)
                {
                    //Start single axis's relative position motion.
                    Result = Motion.mAcm_AxMoveRel(m_Axishand[AxisIndex], movevalue);
                }
                else
                {
                    //Start single axis's absolute position motion.
                    Result = Motion.mAcm_AxMoveAbs(m_Axishand[AxisIndex], movevalue);
                }
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    strTemp = "PTP Move Failed With Error Code: [0x" + Convert.ToString(Result, 16) + "]";
                    ShowMessages(strTemp, Result);
                }
            }
            if (!Rel)
            {
                //更新轴运动目标
                switch (AxisIndex)
                {
                    case 0://axis X
                        Target_X = movevalue / GlobalVar.ServCMDRate;
                        break;
                    case 1://axis Y
                        Target_Y = movevalue / GlobalVar.ServCMDRate;
                        break;
                    case 4://axis A
                        Target_A = movevalue / GlobalVar.ServCMDRate;
                        break;
                    case 5://axis B
                        Target_B = movevalue / GlobalVar.ServCMDRate;
                        break;
                    case 6://axis C
                        Target_C = movevalue / GlobalVar.MotorRate;
                        break;
                    case 7://axis D
                        Target_D = movevalue / GlobalVar.MotorRate;
                        break;
                }
            }
        }
        #endregion

        #region Continuous
        /// <summary>
        /// 持续运动
        /// </summary>
        /// <param name="AxisIndex">轴编号</param>
        /// <param name="DIR">是否正向运动</param>
        internal void MoveContinous(int AxisIndex, bool Dir)
        {
            if (EMGRelease.Elapsed.TotalSeconds < 2) return;//急停松开时间小于两秒，忽略运动
            if (!MoveEnbale) throw new Exception("未完成复位，禁止运动！");

            string strTemp;
            UInt32 Result;
            if (m_bInit)
            {
                //To command axis to make a never ending movement with a specified velocity.1: Negative direction.
                if (Dir) Result = Motion.mAcm_AxMoveVel(m_Axishand[AxisIndex], 0);
                else Result = Motion.mAcm_AxMoveVel(m_Axishand[AxisIndex], 1);

                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    strTemp = "Move Failed With Error Code[0x" + Convert.ToString(Result, 16) + "]";
                    ShowMessages(strTemp, Result);
                    return;
                }
            }
        }
        #endregion

        /// <summary>
        /// 停止运动
        /// </summary>
        /// <param name="AxisIndex">轴序号</param>
        internal void StopMove(int AxisIndex)
        {
            uint Result;
            string strTemp;
            if (m_bInit)
            {
                //To command axis to decelerate to stop.
                Result = Motion.mAcm_AxStopDec(m_Axishand[AxisIndex]);
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    strTemp = "Axis To decelerate Stop Failed With Error Code: [0x" + Convert.ToString(Result, 16) + "]";
                    ShowMessages(strTemp, Result);
                    return;
                }
            }
        }

        /// <summary>
        /// 全部轴停止运动
        /// </summary>
        internal void StopAllMove()
        {
            uint Result;
            string strTemp;
            for (
                int i = 0; i < m_ulAxisCount; i++)
            {
                //To command axis to decelerate to stop.
                Result = Motion.mAcm_AxStopDec(m_Axishand[i]);
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    strTemp = "Axis To decelerate Stop Failed With Error Code: [0x" + Convert.ToString(Result, 16) + "]";
                    ShowMessages(strTemp, Result);
                    return;
                }
            }
        }

        /// <summary>
        /// 立刻停止全部的轴运动
        /// </summary>
        internal void StopAllEMGMove()
        {
            uint Result;
            string strTemp;
            if (m_bInit)
            {
                for (int i = 0; i < m_ulAxisCount; i++)
                {
                    //To command axis to decelerate to stop.
                    Result = Motion.mAcm_AxStopEmg(m_Axishand[i]);
                    if (Result != (uint)ErrorCode.SUCCESS)
                    {
                        strTemp = "Axis To decelerate Stop Failed With Error Code: [0x" + Convert.ToString(Result, 16) + "]";
                        ShowMessages(strTemp, Result);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 立刻停止运动
        /// </summary>
        /// <param name="AxisIndex">轴序号</param>
        internal void StopEMGMove(int AxisIndex)
        {
            uint Result;
            string strTemp;
            if (m_bInit)
            {
                //To command axis to decelerate to stop.
                Result = Motion.mAcm_AxStopEmg(m_Axishand[AxisIndex]);
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    strTemp = "Axis To decelerate Stop Failed With Error Code: [0x" + Convert.ToString(Result, 16) + "]";
                    ShowMessages(strTemp, Result);
                    return;
                }
            }
        }

        /// <summary>
        /// 定点运动---x轴距离-m_LPValue_AxisX    y轴距离-m_LPValue_AxisY
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="RelMove">是否相对运动</param>
        public void FixPointMotion(double x, double y, bool RelMove = true)
        {
            if (!MoveEnbale) throw new Exception("未完成复位，禁止运动！");
            if (RelMove)
            {
                if (x + Position_X > GlobalVar.AxisXRange.MAX) throw new Exception("群组运动，X轴超正极限，禁止运动");
                else if (x + Position_X < GlobalVar.AxisXRange.MIN) throw new Exception("群组运动，X轴超负极限，禁止运动");

                if (y + Position_Y > GlobalVar.AxisYRange.MAX) throw new Exception("群组运动，Y轴超正极限，禁止运动");
                else if (y + Position_Y < GlobalVar.AxisYRange.MIN) throw new Exception("群组运动，Y轴超负极限，禁止运动");
            }
            else
            {
                if (x > GlobalVar.AxisYRange.MAX) throw new Exception("群组运动，X轴超正极限，禁止运动");
                else if (x < GlobalVar.AxisYRange.MIN) throw new Exception("群组运动，X轴超负极限，禁止运动");

                if (y > GlobalVar.AxisYRange.MAX) throw new Exception("群组运动，Y轴超正极限，禁止运动");
                else if (y < GlobalVar.AxisYRange.MIN) throw new Exception("群组运动，Y轴超负极限，禁止运动");
            }

            //double CurPos = new double();
            //获取X轴的当前理论位置            
            //Motion.mAcm_AxGetCmdPosition(m_Axishand[GlobalVar.AxisX.LinkIndex], ref CurPos);
            //Motion.mAcm_AxGetCmdPosition(m_Axishand[GlobalVar.AxisY.LinkIndex], ref CurPos);

            //获取轴的当前实际位置      
            //Motion.mAcm_AxGetActualPosition(m_Axishand[GlobalVar.AxisX.LinkIndex], ref CurPos);
            //m_CmdPosition_X = CurPos;
            //Motion.mAcm_AxGetActualPosition(m_Axishand[GlobalVar.AxisY.LinkIndex], ref CurPos);
            //m_CmdPosition_Y = CurPos;

            double dis_x = x * GlobalVar.ServCMDRate; //+ (RelMove ? -m_CmdPosition_X :);            
            double dis_y = y * GlobalVar.ServCMDRate;// + (RelMove ? 0 : -m_CmdPosition_Y);

            SetPoxEnd_X(dis_x / GlobalVar.ServCMDRate);
            SetPoxEnd_Y(dis_y / GlobalVar.ServCMDRate);
            Target_X = dis_x;//更新群组运动目标
            Target_Y = dis_y;
            AxisGroup_Move(true, RelMove);
        }

        //群组运动
        public void AxisGroup_Move(bool isAutoMotion, bool RelMove = true)
        {
            try
            {
                if (EMGRelease.Elapsed.TotalSeconds < 2) return;//急停松开时间小于两秒，忽略运动

                uint AxisNum = new uint();
                UInt16 State = new UInt16();
                if (m_bInit != true) { return; }
                uint Result = Motion.mAcm_GpGetState(m_GpHand, ref State);
                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    if (isAutoMotion) { eve_MotionMsg("轴群组状态异常，运动禁止", true); }
                    else
                    {
                        MessageBox.Show("轴群组状态异常，运动禁止！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    return;
                }
                if (State != (UInt16)GroupState.STA_Gp_Ready)
                {
                    if (isAutoMotion) { eve_MotionMsg("轴群组为非Ready状态，运动禁止", true); }
                    else
                    {
                        MessageBox.Show("设备(轴)为Not Ready状态，请停止后检查再作業！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    return;
                }
                AxisNum = (uint)2;
                if (RelMove)
                {
                    Result = Motion.mAcm_GpMoveLinearRel(m_GpHand, EndArray, ref AxisNum);
                }
                else
                    Result = Motion.mAcm_GpMoveLinearAbs(m_GpHand, EndArray, ref AxisNum);

                if (Result != (uint)ErrorCode.SUCCESS)
                {
                    if (isAutoMotion) { eve_MotionMsg("轴群组运动异常：[0x" + Convert.ToString(Result, 16) + "", true); }
                    else
                    {
                        MessageBox.Show("轴群组运动异常，错误代码：[0x" + Convert.ToString(Result, 16) + "]", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    return;
                }
            }
            catch { }
        }
        /// <summary>
        /// 判断所有轴运动完毕        
        /// </summary>
        /// <returns>true:运动中   false:静止</returns>
        public bool CheckAxisInMoving()
        {
            //Acm_GpGetState 返回ready表示运动完毕
            UInt16 GpState = new UInt16();
            uint result = Motion.mAcm_GpGetState(m_GpHand, ref GpState);
            if (result == 0)
            {
                m_GpAxisStatus = GpState;
                return GpState != 1;
            }
            else
                return true;
        }

        public void WaitAllMoveFinished()
        {
            if (!m_bInit) return;
            Thread.Sleep(10);
            //UInt16 AxState_X = new UInt16();
            //UInt16 AxState_Y = new UInt16();
            UInt16 AxState_D = new UInt16();
            UInt16 AxState_A = new UInt16();
            UInt16 AxState_B = new UInt16();
            UInt16 AxState_C = new UInt16();
            UInt16 GpState = new UInt16();
            for (; ; )
            {
                uint result = Motion.mAcm_GpGetState(m_GpHand, ref GpState);
                if ((result == (uint)ErrorCode.SUCCESS)
                    && (GpState == 1))
                {
                    //Motion.mAcm_AxGetState(m_Axishand[GlobalVar.AxisX.LinkIndex], ref AxState_X);
                    //Motion.mAcm_AxGetState(m_Axishand[GlobalVar.AxisY.LinkIndex], ref AxState_Y);
                    Motion.mAcm_AxGetState(m_Axishand[GlobalVar.AxisA.LinkIndex], ref AxState_A);
                    Motion.mAcm_AxGetState(m_Axishand[GlobalVar.AxisB.LinkIndex], ref AxState_B);
                    Motion.mAcm_AxGetState(m_Axishand[GlobalVar.AxisC.LinkIndex], ref AxState_C);
                    Motion.mAcm_AxGetState(m_Axishand[GlobalVar.AxisD.LinkIndex], ref AxState_D);
                    if (/*(AxState_X == 1) && (AxState_Y == 1) &&*/ (AxState_D == 1) && (AxState_A == 1) && (AxState_B == 1) && (AxState_C == 1))
                    { break; }
                    Thread.Sleep(5);
                }
            }
        }

        /// <summary>
        /// 等待单个轴运动完成
        /// </summary>
        /// <param name="Index"></param>
        public void WaitSigleMoveFinished(int Index)
        {
            if (!m_bInit) return;
            Thread.Sleep(10);
            UInt16 AxState = new UInt16();
            UInt16 GpState = new UInt16();
            for (; ; )
            {
                uint result = Motion.mAcm_GpGetState(m_GpHand, ref GpState);
                if ((result == (uint)ErrorCode.SUCCESS)
                    && (GpState == 1))
                {
                    Motion.mAcm_AxGetState(m_Axishand[Index], ref AxState);
                    if ((AxState == 1))
                    { break; }
                    Thread.Sleep(5);
                }
            }
        }

        private bool m_MoveEnbale = true;
        private bool pcs1;
        private bool pcs2;
        private bool pcs3;
        private bool pcs4;

        /// <summary>
        /// 复位后认为完成初始化，方可运动
        /// </summary>
        internal bool MoveEnbale { get { return m_MoveEnbale; } }

        //开机/恢复急停，复位，回原点
        public void ResetServ()
        {
            Thread thread = new Thread(new ThreadStart(delegate
            {
                if (!m_bInit) return;
                eve_MotionMsg("设备进行原点回归运动中...");
                while (!m_bInit) { Thread.Sleep(20); }
                WaitAllMoveFinished();
                GlobalVar.resetComplete = false;
                bool result = false;
                if (GetSingleDI(FeedLeftCheck, ref result))
                {
                    if (result)
                        GlobalVar.AxisPCI.SetDO(GlobalVar.AxisPCI.CylinderLeftUpper, true);//左吸取吸气开
                }
                if (GetSingleDI(FeedRightCheck, ref result))
                {
                    if (result)
                        GlobalVar.AxisPCI.SetDO(GlobalVar.AxisPCI.CylinderRightUpper, true);//右吸取吸气开
                }
                if (GetSingleDI(DropCheck, ref result))
                {
                    if (result)
                        GlobalVar.AxisPCI.SetDO(GlobalVar.AxisPCI.CylinderDropUpper, true);//下料吸取吸气开
                }
                if (GetSingleDI(PCSCheck1, ref pcs1) && GetSingleDI(PCSCheck2, ref pcs2) && GetSingleDI(PCSCheck3, ref pcs3) && GetSingleDI(PCSCheck4, ref pcs4))
                {
                    if (pcs1 || pcs2 || pcs3 || pcs4)
                        GlobalVar.AxisPCI.SetDO(GlobalVar.AxisPCI.CylinderPCSUpper, true);//PCS吸取吸气开
                }
                GlobalVar.AxisPCI.SetDO(GlobalVar.AxisPCI.CylinderFeed, false);//上料气缸上顶
                GlobalVar.AxisPCI.SetDO(GlobalVar.AxisPCI.CylinderDrop, false);//下料气缸上顶
                GlobalVar.AxisPCI.SetDO(GlobalVar.AxisPCI.CylinderPCS, false);//PCS气缸上顶
                Thread.Sleep(GlobalVar.CylinderSuctionWaitTime);//等待气缸上顶
                SetAxisHomeSpeed();//设置会原点速度
                if (Home(GlobalVar.AxisA.LinkIndex) && Home(GlobalVar.AxisB.LinkIndex)
                //&& Home(GlobalVar.AxisX.LinkIndex) && Home(GlobalVar.AxisY.LinkIndex)//--不复位X，Y轴[20180910 lqz]
                && Home(GlobalVar.AxisC.LinkIndex) && Home(GlobalVar.AxisD.LinkIndex))  //回機械原點
                {
                    WaitAllMoveFinished();
                    Thread.Sleep(500);//确保轴完全停止
                    m_MoveEnbale = true;
                    //MovetoRefPoint();     
                    CheckCylinders();//检查吸取是否有制品
                    GlobalVar.AxisPCI.SetDO(GlobalVar.AxisPCI.CylinderLeftUpper, false);//左吸取吸气关闭
                    GlobalVar.AxisPCI.SetDO(GlobalVar.AxisPCI.CylinderRightUpper, false);//右吸取吸气关闭
                    GlobalVar.AxisPCI.SetDO(GlobalVar.AxisPCI.CylinderPCSUpper, false);//PCS吸取吸气关闭
                    GlobalVar.AxisPCI.SetDO(GlobalVar.AxisPCI.CylinderDropUpper, false);//下料吸取吸气关闭
                    GlobalVar.AxisPCI.SetDO(GlobalVar.AxisPCI.CylinderLeftLower, false);//左吸取吹气关闭
                    GlobalVar.AxisPCI.SetDO(GlobalVar.AxisPCI.CylinderRightLower, false);//右吸取吹气关闭
                    GlobalVar.AxisPCI.SetDO(GlobalVar.AxisPCI.CylinderPCSLower, false);//PCS吸取吹气关闭
                    GlobalVar.AxisPCI.SetDO(GlobalVar.AxisPCI.CylinderDropLower, false);//下料吸取吹气关闭

                    #region 必须触发原点
                    UInt32 iostatus = new UInt32();
                    if (GetIOState(4, ref iostatus))
                    {
                        if (!((iostatus & (uint)Ax_Motion_IO.AX_MOTION_IO_ORG) > 0))
                        {
                            m_MoveEnbale = false;
                            eve_MotionMsg("A轴返回原点失败，轴未准备好...", true);
                            return;
                        }
                    }
                    if (GetIOState(5, ref iostatus))
                    {
                        if (!((iostatus & (uint)Ax_Motion_IO.AX_MOTION_IO_ORG) > 0))
                        {
                            m_MoveEnbale = false;
                            eve_MotionMsg("B轴返回原点失败，轴未准备好...", true);
                            return;
                        }
                    }
                    #region 复位完成判断--不用
                    //if (GetIOState(0, ref iostatus))
                    //{
                    //    if (!((iostatus & (uint)Ax_Motion_IO.AX_MOTION_IO_ORG) > 0))
                    //    {
                    //        m_MoveEnbale = false;
                    //        eve_MotionMsg("X轴返回原点失败，轴未准备好...", true);
                    //        return;
                    //    }
                    //}
                    //if (GetIOState(1, ref iostatus))
                    //{
                    //    if (!((iostatus & (uint)Ax_Motion_IO.AX_MOTION_IO_ORG) > 0))
                    //    {
                    //        m_MoveEnbale = false;
                    //        eve_MotionMsg("Y轴返回原点失败，轴未准备好...", true);
                    //        return;
                    //    }
                    //}
                    //if (GetIOState(6, ref iostatus))
                    //{
                    //    if (!((iostatus & (uint)Ax_Motion_IO.AX_MOTION_IO_ORG) > 0))
                    //    {
                    //        m_MoveEnbale = false;
                    //        eve_MotionMsg("C轴返回原点失败，轴未准备好...", true);
                    //        return;
                    //    }
                    //}
                    //if (GetIOState(7, ref iostatus))
                    //{
                    //    if (!((iostatus & (uint)Ax_Motion_IO.AX_MOTION_IO_ORG) > 0))
                    //    {
                    //        m_MoveEnbale = false;
                    //        eve_MotionMsg("D轴返回原点失败，轴未准备好...", true);
                    //        return;
                    //    }
                    //}
                    #endregion

                    #endregion
                    MoveToRef();//所有轴运动到参考原点
                    WaitAllMoveFinished();
                    //if (Position_A==Target_A&& Position_B==Target_B&&Position_C==Target_C&&Position_D==Target_D&&Position_X==Target_X&&Position_Y==Target_Y)
                    eve_MotionMsg("设备运动到参考原点，轴准备好...");
                    ////  else
                    // MsgBox("设备回原点操作失败，请重新复位!!", System.Drawing.Color.Red, MessageBoxButtons.OK);
                    GlobalVar.resetComplete = true;
                }
                else
                {
                    MsgBox("设备回原点操作失败，请重新启动上位机!", System.Drawing.Color.Red, MessageBoxButtons.OK);
                    CloseBoard();
                    GlobalVar.PCS_Port.ClosePCSPort();//关闭串口
                    Application.Exit();
                }
            }));
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// 检查真空
        /// </summary>
        public void CheckCylinders()
        {
            bool PcsSingle1 = false;
            bool PcsSingle2 = false;
            bool PcsSingle3 = false;
            bool PcsSingle4 = false;
            bool FeedSingleLeft = false;
            bool FeedSingleRight = false;
            bool DropSingle = false;
            //PCS吸取有制品
            if (GetSingleDI(PCSCheck1, ref PcsSingle1) && GetSingleDI(PCSCheck2, ref PcsSingle2) && GetSingleDI(PCSCheck3, ref PcsSingle3) && GetSingleDI(PCSCheck4, ref PcsSingle4))
            {
                eve_MotionMsg("检查PCS吸取是否有制品");
                if (PcsSingle4 || PcsSingle3 || PcsSingle2 || PcsSingle1)//pcs吸取只要有一个真空就要接制品
                {
                    MoveDIR(GlobalVar.AxisD.LinkIndex, true, GlobalVar.Point_PCSPhotoPosition * GlobalVar.MotorRate, false);
                    WaitSigleMoveFinished(GlobalVar.AxisD.LinkIndex);
                    SuckerMotion(5, false);
                    MoveDIR(GlobalVar.AxisD.LinkIndex, true, GlobalVar.Point_PCSFeed * GlobalVar.MotorRate, false);
                    WaitSigleMoveFinished(GlobalVar.AxisD.LinkIndex);
                    MsgBox("PCS气缸有制品，请处理!", System.Drawing.Color.Orange, MessageBoxButtons.OK);
                }
            }
            else
                eve_MotionMsg("获取PCS吸取状态失败！");
            //上料真空有托盘
            if (GetSingleDI(FeedLeftCheck, ref FeedSingleLeft) && GetSingleDI(FeedRightCheck, ref FeedSingleRight))
            {
                eve_MotionMsg("检查上料轴吸取是否有制品");
                if (FeedSingleRight || FeedSingleLeft)
                {
                    MoveDIR(GlobalVar.AxisA.LinkIndex, true, GlobalVar.Point_FeedLeft * GlobalVar.ServCMDRate, false);
                    MoveDIR(GlobalVar.AxisC.LinkIndex, true, GlobalVar.Point_ICFeed * GlobalVar.MotorRate, false);
                    WaitSigleMoveFinished(GlobalVar.AxisC.LinkIndex);
                    WaitSigleMoveFinished(GlobalVar.AxisA.LinkIndex);
                    SuckerMotion(4, false);
                    MsgBox("上料轴有制品，请处理!", System.Drawing.Color.Orange, MessageBoxButtons.OK);
                }
            }
            else
                eve_MotionMsg("获取上料轴吸取状态失败！");

            if (GetSingleDI(DropCheck, ref DropSingle))
            {
                eve_MotionMsg("检查下料轴吸取是否有制品");
                if (DropSingle)
                {
                    MoveDIR(GlobalVar.AxisB.LinkIndex, true, GlobalVar.Point_DropRight * GlobalVar.ServCMDRate, false);
                    WaitSigleMoveFinished(GlobalVar.AxisB.LinkIndex);
                    SuckerMotion(3, false);
                }
            }
            else
                eve_MotionMsg("获取下料轴吸取状态失败！");
        }

        //只允许从机械原点开始运动向参考原点
        public void MovetoRefPoint()
        {
            try
            {

                //设置为较慢回归速度
                // SetProp_GPSpeed(GlobalVar.m_GPValue_VelHigh_low, GlobalVar.m_GPValue_VelLow_low,
                // GlobalVar.m_GPValue_Acc_low, GlobalVar.m_GPValue_Dec_low);
                SetPoxEnd_X(Convert.ToDouble(GlobalVar.Ref_Point_AxisX));
                SetPoxEnd_Y(Convert.ToDouble(GlobalVar.Ref_Point_AxisY));
                AxisGroup_Move(true, false);

                WaitAllMoveFinished();
                Thread.Sleep(200);
            }
            catch { }
        }

        public void MoveToRef()
        {
            try
            {
                #region 设置轴速度
                SetProp_VelHigh((uint)GlobalVar.AxisA.LinkIndex, GlobalVar.RefHighVel, false);
                SetProp_VelLow((uint)GlobalVar.AxisA.LinkIndex, GlobalVar.RefLowVel, false);
                SetProp_Acc((uint)GlobalVar.AxisA.LinkIndex, GlobalVar.RefAccVel, false);
                SetProp_Dec((uint)GlobalVar.AxisA.LinkIndex, GlobalVar.RefDccVel, false);
                SetProp_VelHigh((uint)GlobalVar.AxisB.LinkIndex, GlobalVar.RefHighVel, false);
                SetProp_VelLow((uint)GlobalVar.AxisB.LinkIndex, GlobalVar.RefLowVel, false);
                SetProp_Acc((uint)GlobalVar.AxisB.LinkIndex, GlobalVar.RefAccVel, false);
                SetProp_Dec((uint)GlobalVar.AxisB.LinkIndex, GlobalVar.RefDccVel, false);
                #endregion
                //设置为较慢回归速度
                SetProp_GPSpeed(GlobalVar.m_GPValue_VelHigh_low, GlobalVar.m_GPValue_VelLow_low,
                    GlobalVar.m_GPValue_Acc_low, GlobalVar.m_GPValue_Dec_low);
                SetPoxEnd_X(Convert.ToDouble(GlobalVar.Ref_Point_AxisX));
                SetPoxEnd_Y(Convert.ToDouble(GlobalVar.Ref_Point_AxisY));
                AxisGroup_Move(true, false);
                MoveDIR(GlobalVar.AxisA.LinkIndex, true, GlobalVar.Point_FeedLeft * GlobalVar.ServCMDRate, false);
                MoveDIR(GlobalVar.AxisB.LinkIndex, true, GlobalVar.Point_DropRight * GlobalVar.ServCMDRate, false);
                MoveDIR(GlobalVar.AxisC.LinkIndex, true, GlobalVar.Point_ICFeed * GlobalVar.MotorRate, false);
                MoveDIR(GlobalVar.AxisD.LinkIndex, true, GlobalVar.Point_PCSFeed * GlobalVar.MotorRate, false);
                WaitAllMoveFinished();
                SetAxisRunSpeed();
            }
            catch (Exception ex) { }
        }
        /// <summary>
        /// 设置回原点速度
        /// </summary>
        public void SetAxisHomeSpeed()
        {//设置运行速度
            SetProp_VelHigh((uint)GlobalVar.AxisA.LinkIndex, GlobalVar.HomeSpeed, false);
            SetProp_VelLow((uint)GlobalVar.AxisA.LinkIndex, GlobalVar.HomeSpeed / 2, false);
            SetProp_Acc((uint)GlobalVar.AxisA.LinkIndex, GlobalVar.HomeSpeed * 2, false);
            SetProp_Dec((uint)GlobalVar.AxisA.LinkIndex, GlobalVar.HomeSpeed * 2, false);
            SetProp_VelHigh((uint)GlobalVar.AxisB.LinkIndex, GlobalVar.HomeSpeed, false);
            SetProp_VelLow((uint)GlobalVar.AxisB.LinkIndex, GlobalVar.HomeSpeed / 2, false);
            SetProp_Acc((uint)GlobalVar.AxisB.LinkIndex, GlobalVar.HomeSpeed * 2, false);
            SetProp_Dec((uint)GlobalVar.AxisB.LinkIndex, GlobalVar.HomeSpeed * 2, false);
            SetProp_VelHigh((uint)GlobalVar.AxisC.LinkIndex, 80000, false);
            SetProp_VelLow((uint)GlobalVar.AxisC.LinkIndex, 10000, false);
            SetProp_Acc((uint)GlobalVar.AxisC.LinkIndex, 80000, false);
            SetProp_Dec((uint)GlobalVar.AxisC.LinkIndex, 80000, false);
            SetProp_VelHigh((uint)GlobalVar.AxisD.LinkIndex, 80000, false);
            SetProp_VelLow((uint)GlobalVar.AxisD.LinkIndex, 10000, false);
            SetProp_Acc((uint)GlobalVar.AxisD.LinkIndex, 80000, false);
            SetProp_Dec((uint)GlobalVar.AxisD.LinkIndex, 80000, false);
        }
        /// <summary>
        /// 设置轴的运动速度
        /// </summary>
        public void SetAxisRunSpeed()
        {
            //设置运行速度
            SetProp_VelHigh((uint)GlobalVar.AxisA.LinkIndex, GlobalVar.RunHighVel, false);
            SetProp_VelLow((uint)GlobalVar.AxisA.LinkIndex, GlobalVar.RunLowVel, false);
            SetProp_Acc((uint)GlobalVar.AxisA.LinkIndex, GlobalVar.RunAccVel, false);
            SetProp_Dec((uint)GlobalVar.AxisA.LinkIndex, GlobalVar.RunDccVel, false);
            SetProp_VelHigh((uint)GlobalVar.AxisB.LinkIndex, GlobalVar.RunHighVel, false);
            SetProp_VelLow((uint)GlobalVar.AxisB.LinkIndex, GlobalVar.RunLowVel, false);
            SetProp_Acc((uint)GlobalVar.AxisB.LinkIndex, GlobalVar.RunAccVel, false);
            SetProp_Dec((uint)GlobalVar.AxisB.LinkIndex, GlobalVar.RunDccVel, false);
            SetProp_VelHigh((uint)GlobalVar.AxisC.LinkIndex, GlobalVar.RunHighVel_Motor, false);
            SetProp_VelLow((uint)GlobalVar.AxisC.LinkIndex, GlobalVar.RunLowVel_Motor, false);
            SetProp_Acc((uint)GlobalVar.AxisC.LinkIndex, GlobalVar.RunAccVel_Motor, false);
            SetProp_Dec((uint)GlobalVar.AxisC.LinkIndex, GlobalVar.RunDccVel_Motor, false);
            SetProp_VelHigh((uint)GlobalVar.AxisD.LinkIndex, GlobalVar.RunHighVel_Motor, false);
            SetProp_VelLow((uint)GlobalVar.AxisD.LinkIndex, GlobalVar.RunLowVel_Motor, false);
            SetProp_Acc((uint)GlobalVar.AxisD.LinkIndex, GlobalVar.RunAccVel_Motor, false);
            SetProp_Dec((uint)GlobalVar.AxisD.LinkIndex, GlobalVar.RunDccVel_Motor, false);
        }

        public void SetAxisOperateSpeed()
        {
            //设置有制品运行速度
            SetProp_VelHigh((uint)GlobalVar.AxisA.LinkIndex, GlobalVar.RunHighVel_Operate, false);
            SetProp_VelLow((uint)GlobalVar.AxisA.LinkIndex, GlobalVar.RunLowVel_Operate, false);
            SetProp_Acc((uint)GlobalVar.AxisA.LinkIndex, GlobalVar.RunAccVel_Operate, false);
            SetProp_Dec((uint)GlobalVar.AxisA.LinkIndex, GlobalVar.RunDccVel_Operate, false);
            SetProp_VelHigh((uint)GlobalVar.AxisB.LinkIndex, GlobalVar.RunHighVel_Operate, false);
            SetProp_VelLow((uint)GlobalVar.AxisB.LinkIndex, GlobalVar.RunLowVel_Operate, false);
            SetProp_Acc((uint)GlobalVar.AxisB.LinkIndex, GlobalVar.RunAccVel_Operate, false);
            SetProp_Dec((uint)GlobalVar.AxisB.LinkIndex, GlobalVar.RunDccVel_Operate, false);
        }



        /// <summary>
        /// 吸盘动作
        /// </summary>
        /// <param name="SuckerNum">吸盘序号   1:上料轴左吸取; 2:上料轴右吸取; 3:下料轴吸取; 4:上料轴左右吸取;  5:PCS吸取</param>
        /// <param name="isSuck">吸取或放置  true：吸取 ; false: 放置</param>
        internal void SuckerMotion(int SuckerNum, bool isSuck)
        {
            bool suck = false, blow = false;
            if (isSuck)
            {
                suck = true;//吸取打开
                blow = false;//吹气关闭
            }
            else
            {
                suck = false;//吸取关闭
                blow = true;//吹气打开
            }
            switch (SuckerNum)
            {
                case 1:
                    SetDO(CylinderFeed, true);//气缸下降
                    SetDO(CylinderLeftUpper, suck);//上料左吸取
                    //Thread.Sleep(GlobalVar.CylinderSuctionWaitTime);
                    for (int i = 0; i < 5; i++)
                    {
                        bool under = false;
                        GetSingleDI(FeedCylinderUnder, ref under);
                        if (under) break;
                        Thread.Sleep(200);
                    }
                    SetDO(CylinderLeftLower, blow);//上料左吹气
                    Thread.Sleep(100);//等待延时
                    SetDO(CylinderFeed, false);//气缸上升
                    if (!isSuck)//放置动作
                    {
                        SetDO(CylinderLeftLower, !blow);//上料左吹气
                    }
                    for (int j=0;j<3;j++)
                    {
                        bool lmt = false;
                        GetSingleDI(FeedCylinderUpper, ref lmt);
                        if (lmt) break;
                        Thread.Sleep(300);
                    }
                    //Thread.Sleep(GlobalVar.CylinderBlowWaitTime);
                    break; 
                case 2:
                    SetDO(CylinderFeed, true);//气缸下降                   
                    //Thread.Sleep(GlobalVar.CylinderSuctionWaitTime);
                    SetDO(CylinderRightUpper, suck);//上料右吸取
                    for (int i = 0; i < 5; i++)
                    {
                        bool under = false;
                        GetSingleDI(FeedCylinderUnder, ref under);
                        if (under) break;
                        Thread.Sleep(200);
                    }
                    SetDO(CylinderRightLower, blow);//上料右吹气
                    Thread.Sleep(100);//等待延时
                    if (!isSuck)
                        SetDO(CylinderRightLower, !blow);//上料右吹气
                    SetDO(CylinderFeed, false);//气缸上升  
                    for (int j = 0; j < 3; j++)
                    {
                        bool lmt = false;
                        GetSingleDI(FeedCylinderUpper, ref lmt);
                        if (lmt) break;
                        Thread.Sleep(300);
                    }
                    //Thread.Sleep(GlobalVar.CylinderBlowWaitTime);
                    break;
                case 3:
                    SetDO(CylinderDrop, true);//气缸下降
                                              //Thread.Sleep(GlobalVar.CylinderSuctionWaitTime);
                    SetDO(CylinderDropUpper, suck);//下料吸取
                    for (int i = 0; i < 5; i++)
                    {
                        bool under = false;
                        GetSingleDI(FeedCylinderUnder, ref under);
                        if (under) break;
                        Thread.Sleep(200);
                    }
                    SetDO(CylinderDropLower, blow);//下料吹气
                    Thread.Sleep(100);//等待延时
                    if (!isSuck)
                        SetDO(CylinderDropLower, !blow);//下料吹气
                    SetDO(CylinderDrop, false);//气缸上升
                    for (int j = 0; j < 3; j++)
                    {
                        bool lmt = false;
                        GetSingleDI(DropCylinderUpper, ref lmt);
                        if (lmt) break;
                        Thread.Sleep(300);
                    }
                    //Thread.Sleep(GlobalVar.CylinderBlowWaitTime);
                    break;
                case 4:
                    SetDO(CylinderFeed, true);//气缸下降
                    //Thread.Sleep(GlobalVar.CylinderSuctionWaitTime);
                    SetDO(CylinderLeftUpper, suck);//上料左吸取
                    SetDO(CylinderRightUpper, suck);//上料右吸取
                    for (int i = 0; i < 5; i++)
                    {
                        bool under = false;
                        GetSingleDI(FeedCylinderUnder, ref under);
                        if (under) break;
                        Thread.Sleep(200);
                    }
                    SetDO(CylinderRightLower, blow);//上料右吹气
                    SetDO(CylinderLeftLower, blow);//上料左吹气
                    Thread.Sleep(100);//等待延时
                    if (!isSuck)
                    {
                        SetDO(CylinderRightLower, !blow);//上料右吹气
                        SetDO(CylinderLeftLower, !blow);//上料左吹气
                    }
                    SetDO(CylinderFeed, false);//气缸上升
                    for (int j = 0; j < 3; j++)
                    {
                        bool lmt = false;
                        GetSingleDI(FeedCylinderUpper, ref lmt);
                        if (lmt) break;
                        Thread.Sleep(300);
                    }
                    //Thread.Sleep(GlobalVar.CylinderBlowWaitTime);
                    break;
                case 5:
                    SetDO(CylinderPCS, true);//pcs气缸下降
                    Thread.Sleep(1300);
                    SetDO(CylinderPCSUpper, suck);//pcs吸气
                    SetDO(CylinderPCSLower, blow);//pcs吹气
                    Thread.Sleep(100);//等待延时
                    if (!isSuck) SetDO(CylinderPCSLower, !blow);//pcs吹气
                    Thread.Sleep(100);
                    SetDO(CylinderPCS, false);//pcs气缸上升
                    Thread.Sleep(GlobalVar.CylinderBlowWaitTime);
                    break;
                default:
                    throw new Exception("吸取操作异常:不存在当前吸盘");
            }
        }
    }
}
