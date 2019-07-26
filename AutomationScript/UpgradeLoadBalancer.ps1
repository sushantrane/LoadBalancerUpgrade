#PROVIDED AS WEBHOOK ONCE READY FOR PRODUCTION USE

$rgName = ""
$lbName = ""

$basicLB = Get-AzLoadBalancer -ResourceGroupName $rgName -Name $lbName

if($basicLB.Sku.Name -eq "Basic")
{ 
    #$checkforPublicIP publicIpAvailable
    $checkforPublicIP = $basicLb.FrontendIpConfigurations | Where-Object {$_.PublicIpAddress -ne $null}
    if($null -eq $checkforPublicIP)
    {
        #Store the Basic LB Config

        #Delete the Basic LB
        Remove-AzLoadBalancer -Name $lbName -ResourceGroupName $rgName -Force

        #Create New Standard LB
        $stdLb = New-AzLoadBalancer -ResourceGroupName $rgName -Name $lbName -Location $basicLB.Location -Tag $basicLB.Tag -Sku Standard

        #create Front end configs
        foreach($frontendConfig in $basicLB.FrontendIpConfigurations)
        {
            $feip = New-AzLoadBalancerFrontendIpConfig -Name $frontendConfig.Name -PrivateIpAddress $frontendConfig.PrivateIpAddress -SubnetId $frontendConfig.Subnet.Id
            $stdLb | Add-AzLoadBalancerFrontendIpConfig -Name $frontendConfig.Name -PrivateIpAddress $frontendConfig.PrivateIpAddress -SubnetId $frontendConfig.Subnet.Id | Set-AzLoadBalancer
        }

        #Create BackendPools
        foreach($backendpool in $basicLB.BackendAddressPools)
        {
            $stdLb | Add-AzLoadBalancerBackendAddressPoolConfig -Name $backendpool.Name | Set-AzLoadBalancer
        }

        #create Probes
        foreach($probe in $basicLB.Probes)
        {
            if($probe.RequestPath)
            {
                $stdLb | Add-AzLoadBalancerProbeConfig -Name $probe.Name -RequestPath $probe.RequestPath -Protocol $probe.Protocol -Port $probe.Port -IntervalInSeconds $probe.IntervalInSeconds -ProbeCount $probe.NumberOfProbes | Set-AzLoadBalancer
            }
            else
            {
                $stdLb | Add-AzLoadBalancerProbeConfig -Name $probe.Name  -Protocol $probe.Protocol -Port $probe.Port -IntervalInSeconds $probe.IntervalInSeconds -ProbeCount $probe.NumberOfProbes | Set-AzLoadBalancer
            }
        }

        foreach($rule in $basicLB.LoadBalancingRules)
        {
            #Get the FrontendConfig of the existing rule

            $existingFrontEndIPConfig = $rule.FrontendIPConfiguration.Id 
            $feip = Get-AzLoadBalancerFrontendIpConfig -LoadBalancer $stdLb -Name $existingFrontEndIPConfig.Split("/")[10]
            $bePoolName = $rule.BackendAddressPool.Id.Split("/")[10]
            $stdbepool = $stdLb.BackendAddressPools | Where-Object {$_.Name -eq $bePoolName}

            $probe = Get-AzLoadBalancerProbeConfig -LoadBalancer $stdLb -Name $rule.Probe.Id.Split("/")[10]


            if($rule.EnableFloatingIP)
            {
                $stdLb | Add-AzLoadBalancerRuleConfig -Name $rule.Name -FrontendIPConfiguration $feip -Protocol $rule.Protocol -FrontendPort $rule.FrontendPort -BackendPort $rule.BackendPort -LoadDistribution $rule.LoadDistribution -IdleTimeoutInMinutes $rule.IdleTimeoutInMinutes -EnableFloatingIP -Probe $probe -BackendAddressPool $stdbepool
                
            }
            else
            {
                $stdLb | Add-AzLoadBalancerRuleConfig -Name $rule.Name -FrontendIPConfiguration $feip -Protocol $rule.Protocol -FrontendPort $rule.FrontendPort -BackendPort $rule.BackendPort -LoadDistribution $rule.LoadDistribution -IdleTimeoutInMinutes $rule.IdleTimeoutInMinutes -Probe $probe -BackendAddressPool $stdbepool
            }
            
            $stdLb | Set-AzLoadBalancer
        }

        foreach($natPool in $basicLB.InboundNatPools)
        {
            $existingFrontEndIPConfig = $rule.FrontendIPConfiguration.Id 
            $feip = Get-AzLoadBalancerFrontendIpConfig -LoadBalancer $stdLb -Name $existingFrontEndIPConfig.Split("/")[10]
            if($natPool.EnableFloatingIP)
            {
                $stdLb | Add-AzLoadBalancerInboundNatPoolConfig -Name $natPool.Name -Protocol $natPool.Protocol -FrontendIPConfigurationId $feip.Id -FrontendPortRangeStart $natPool.FrontendPortRangeStart. -FrontendPortRangeEnd $natPool.FrontendPortRangeEnd -BackendPort $natPool.BackendPort -EnableFloatingIP
            }
            else
            {
                $stdLb | Add-AzLoadBalancerInboundNatPoolConfig -Name $natPool.Name -Protocol $natPool.Protocol -FrontendIPConfigurationId $feip.Id -FrontendPortRangeStart $natPool.FrontendPortRangeStart. -FrontendPortRangeEnd $natPool.FrontendPortRangeEnd -BackendPort $natPool.BackendPort -EnableFloatingIP
            }

            $stdLb | Set-AzLoadBalancer
            
        }

        foreach($natRule in $basicLB.InboundNatRules)
        {
            #$stdnatpool = $stdLb.InboundNatRules | Where-Object {$_.Name -eq $natRule.Name}

            $existingFrontEndIPConfig = $natRule.FrontendIPConfiguration.Id 

            $feip = Get-AzLoadBalancerFrontendIpConfig -LoadBalancer $stdLb -Name $existingFrontEndIPConfig.Split("/")[10]

            if($natRule.EnableFloatingIP)
            {
                $stdLb | Add-AzLoadBalancerInboundNatRuleConfig -Name $natRule.Name -FrontendIPConfiguration $feip -Protocol $natRule.Protocol -FrontendPort $natRule.FrontendPort -BackendPort 3350 -EnableFloatingIP -IdleTimeoutInMinutes $natRule.IdleTimeoutInMinutes| Set-AzLoadBalancer
            }
            else
            {
                $stdLb | Add-AzLoadBalancerInboundNatRuleConfig -Name $natRule.Name -FrontendIPConfiguration $feip -Protocol $natRule.Protocol -FrontendPort $natRule.FrontendPort -BackendPort 3350 -IdleTimeoutInMinutes $natRule.IdleTimeoutInMinutes | Set-AzLoadBalancer
            }

            

            $nic = Get-AzNetworkInterface -ResourceGroupName $natRule.BackendIPConfiguration.Id.Split("/")[4] -Name $natRule.BackendIPConfiguration.Id.Split("/")[8]
            $nicIPConfig = Get-AzNetworkInterfaceIpConfig -Name $natRule.BackendIPConfiguration.Id.Split("/")[10] -NetworkInterface $nic

            $vnet = Get-AzVirtualNetwork -Name $nicIPConfig.Subnet.Id.Split("/")[8] -ResourceGroupName $nicIPConfig.Subnet.Id.Split("/")[4]
            $subnet = Get-AzVirtualNetworkSubnetConfig -Name $nicIPConfig.Subnet.Id.Split("/")[10] -VirtualNetwork $vnet

            $stdLBNatRule = Get-AzLoadBalancerInboundNatRuleConfig -LoadBalancer $stdLb -Name $natRule.Name

            $nic | Set-AzNetworkInterfaceIpConfig -Name $natRule.BackendIPConfiguration.Id.Split("/")[10] -LoadBalancerInboundNatRule $stdLBNatRule -Subnet $subnet

            Set-AzNetworkInterface -NetworkInterface $nic
            
        }

        $stdLb = Get-AzLoadBalancer -ResourceGroupName $rgName -Name $lbName

        foreach($bepool in $basicLB.BackendAddressPools)
        {
            $stdbepool = $stdLb.BackendAddressPools | Where-Object {$_.Name -eq $bepool.Name}
            foreach($beip in $bepool.BackendIpConfigurations)
            {
                $nic = Get-AzNetworkInterface -ResourceGroupName $beip.Id.Split("/")[4] -Name $beip.Id.Split("/")[8]
                $nicIPConfig = Get-AzNetworkInterfaceIpConfig -Name $beip.Id.Split("/")[10] -NetworkInterface $nic
                $vnet = Get-AzVirtualNetwork -Name $nicIPConfig.Subnet.Id.Split("/")[8] -ResourceGroupName $nicIPConfig.Subnet.Id.Split("/")[4]
                $subnet = Get-AzVirtualNetworkSubnetConfig -Name $nicIPConfig.Subnet.Id.Split("/")[10] -VirtualNetwork $vnet


                $nic | Set-AzNetworkInterfaceIpConfig -Name $beip.Id.Split("/")[10] -LoadBalancerBackendAddressPool $stdbepool -Subnet $subnet
                Set-AzNetworkInterface -NetworkInterface $nic
            }
        }
    }
}