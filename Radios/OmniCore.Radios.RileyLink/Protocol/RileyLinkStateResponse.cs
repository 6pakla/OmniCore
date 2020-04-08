﻿namespace OmniCore.Radios.RileyLink.Protocol
{
    public class RileyLinkStateResponse : RileyLinkStandardResponse
    {
        public bool StateOk { get; private set; }

        protected override void ParseInternal(byte[] responseData)
        {
            StateOk = responseData.Length == 2 &&
                      responseData[0] == 'O' &&
                      responseData[1] == 'K';
        }
    }
}