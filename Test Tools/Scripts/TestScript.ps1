#PROVIDED AS WEBHOOK ONCE READY FOR PRODUCTION USE

$rgName = ""
$lbName = ""


$basicLB = Get-AzLoadBalancer -ResourceGroupName $rgName -Name $lbName

if (($basicLB.Sku.Name -eq "Basic") -and !($basicLB.InboundNatPools)) { 
    #$checkforPublicIP publicIpAvailable
    $checkforPublicIP = $basicLb.FrontendIpConfigurations | Where-Object { $_.PublicIpAddress -ne $null }
    if ($null -eq $checkforPublicIP) {
        #Store the Basic LB Config

        #Delete the Basic LB
        Remove-AzLoadBalancer -Name $lbName -ResourceGroupName $rgName -Force

        #Create New Standard LB
        $stdLb = New-AzLoadBalancer -ResourceGroupName $rgName -Name $lbName -Location $basicLB.Location -Tag $basicLB.Tag -Sku Standard

        #create Front end configs
        foreach ($frontendConfig in $basicLB.FrontendIpConfigurations) {
            $feip = New-AzLoadBalancerFrontendIpConfig -Name $frontendConfig.Name -PrivateIpAddress $frontendConfig.PrivateIpAddress -SubnetId $frontendConfig.Subnet.Id
            $stdLb | Add-AzLoadBalancerFrontendIpConfig -Name $frontendConfig.Name -PrivateIpAddress $frontendConfig.PrivateIpAddress -SubnetId $frontendConfig.Subnet.Id | Set-AzLoadBalancer
        }

        #Create BackendPools
        foreach ($backendpool in $basicLB.BackendAddressPools) {
            $stdLb | Add-AzLoadBalancerBackendAddressPoolConfig -Name $backendpool.Name | Set-AzLoadBalancer
        }

        #create Probes
        foreach ($probe in $basicLB.Probes) {
            if ($probe.RequestPath) {
                $stdLb | Add-AzLoadBalancerProbeConfig -Name $probe.Name -RequestPath $probe.RequestPath -Protocol $probe.Protocol -Port $probe.Port -IntervalInSeconds $probe.IntervalInSeconds -ProbeCount $probe.NumberOfProbes | Set-AzLoadBalancer
            }
            else {
                $stdLb | Add-AzLoadBalancerProbeConfig -Name $probe.Name  -Protocol $probe.Protocol -Port $probe.Port -IntervalInSeconds $probe.IntervalInSeconds -ProbeCount $probe.NumberOfProbes | Set-AzLoadBalancer
            }
        }

        # Set LB Rules
        foreach ($rule in $basicLB.LoadBalancingRules) {
            #Get the FrontendConfig of the existing rule

            $existingFrontEndIPConfig = $rule.FrontendIPConfiguration.Id 
            $feip = Get-AzLoadBalancerFrontendIpConfig -LoadBalancer $stdLb -Name $existingFrontEndIPConfig.Split("/")[10]
            $bePoolName = $rule.BackendAddressPool.Id.Split("/")[10]
            $stdbepool = $stdLb.BackendAddressPools | Where-Object { $_.Name -eq $bePoolName }

            $probe = Get-AzLoadBalancerProbeConfig -LoadBalancer $stdLb -Name $rule.Probe.Id.Split("/")[10]


            if ($rule.EnableFloatingIP) {
                $stdLb | Add-AzLoadBalancerRuleConfig -Name $rule.Name -FrontendIPConfiguration $feip -Protocol $rule.Protocol -FrontendPort $rule.FrontendPort -BackendPort $rule.BackendPort -LoadDistribution $rule.LoadDistribution -IdleTimeoutInMinutes $rule.IdleTimeoutInMinutes -EnableFloatingIP -Probe $probe -BackendAddressPool $stdbepool
                
            }
            else {
                $stdLb | Add-AzLoadBalancerRuleConfig -Name $rule.Name -FrontendIPConfiguration $feip -Protocol $rule.Protocol -FrontendPort $rule.FrontendPort -BackendPort $rule.BackendPort -LoadDistribution $rule.LoadDistribution -IdleTimeoutInMinutes $rule.IdleTimeoutInMinutes -Probe $probe -BackendAddressPool $stdbepool
            }
            
            $stdLb | Set-AzLoadBalancer
        }

        <# TO DO FOR VMSS
        foreach ($natPool in $basicLB.InboundNatPools) {
            $existingFrontEndIPConfig = $rule.FrontendIPConfiguration.Id 
            $feip = Get-AzLoadBalancerFrontendIpConfig -LoadBalancer $stdLb -Name $existingFrontEndIPConfig.Split("/")[10]
            if ($natPool.EnableFloatingIP) {
                $stdLb | Add-AzLoadBalancerInboundNatPoolConfig -Name $natPool.Name -Protocol $natPool.Protocol -FrontendIPConfigurationId $feip.Id -FrontendPortRangeStart $natPool.FrontendPortRangeStart. -FrontendPortRangeEnd $natPool.FrontendPortRangeEnd -BackendPort $natPool.BackendPort -EnableFloatingIP
            }
            else {
                $stdLb | Add-AzLoadBalancerInboundNatPoolConfig -Name $natPool.Name -Protocol $natPool.Protocol -FrontendIPConfigurationId $feip.Id -FrontendPortRangeStart $natPool.FrontendPortRangeStart. -FrontendPortRangeEnd $natPool.FrontendPortRangeEnd -BackendPort $natPool.BackendPort -EnableFloatingIP
            }

            $stdLb | Set-AzLoadBalancer
            
        }
        #>

        # SET Inbound NAT Rules
        foreach ($natRule in $basicLB.InboundNatRules) {
            $existingFrontEndIPConfig = $natRule.FrontendIPConfiguration.Id 

            $feip = Get-AzLoadBalancerFrontendIpConfig -LoadBalancer $stdLb -Name $existingFrontEndIPConfig.Split("/")[10]

            if ($natRule.EnableFloatingIP) {
                $stdLb | Add-AzLoadBalancerInboundNatRuleConfig -Name $natRule.Name -FrontendIPConfiguration $feip -Protocol $natRule.Protocol -FrontendPort $natRule.FrontendPort -BackendPort 3350 -EnableFloatingIP -IdleTimeoutInMinutes $natRule.IdleTimeoutInMinutes | Set-AzLoadBalancer
            }
            else {
                $stdLb | Add-AzLoadBalancerInboundNatRuleConfig -Name $natRule.Name -FrontendIPConfiguration $feip -Protocol $natRule.Protocol -FrontendPort $natRule.FrontendPort -BackendPort 3350 -IdleTimeoutInMinutes $natRule.IdleTimeoutInMinutes | Set-AzLoadBalancer
            }
        }

        $stdLb = Get-AzLoadBalancer -ResourceGroupName $rgName -Name $lbName

        # Update the NIC IP Configurations for the existing VMs 
        foreach ($bepool in $basicLB.BackendAddressPools) {
            $stdbepool = $stdLb.BackendAddressPools | Where-Object { $_.Name -eq $bepool.Name }
            foreach ($beip in $bepool.BackendIpConfigurations) {
                $nic = Get-AzNetworkInterface -ResourceGroupName $beip.Id.Split("/")[4] -Name $beip.Id.Split("/")[8]
                $nicIPConfig = Get-AzNetworkInterfaceIpConfig -Name $beip.Id.Split("/")[10] -NetworkInterface $nic

                $natRuleforNic = $basicLB.InboundNatRules | Where-Object { $_.BackendIPConfiguration.id -eq $nicIPConfig.id }

                ($nic.IpConfigurations | Where-Object { $_.Id -eq $beip.Id }).LoadBalancerBackendAddressPools = $stdbepool
                ($nic.IpConfigurations | Where-Object { $_.Id -eq $beip.Id }).LoadBalancerInboundNatRules = $natRuleforNic

                Set-AzNetworkInterface -NetworkInterface $nic
            }
        }
    }
}