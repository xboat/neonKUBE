﻿//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleDockerService.Misc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Docker;
using Neon.Xunit;
using Neon.Xunit.Cluster;

using Xunit;

namespace TestNeonCluster
{
    public partial class Test_AnsibleDockerService : IClassFixture<ClusterFixture>
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void CheckArgs()
        {
            //-----------------------------------------------------------------
            // Verify that the module detects unknown top-level arguments.
            
            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: absent
        image: {serviceImage}
        UNKNOWN: argument
";
            var results = AnsiblePlayer.Play(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.False(taskResult.Success);

            //-----------------------------------------------------------------
            // Verify that the module detects unknown config arguments.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: absent
        image: {serviceImage}
        config:
          - source: config-1
            target: config
            uid: {TestHelper.TestUID}
            gid: {TestHelper.TestGID}
            mode: 440
            UNKNOWN: argument
";
            results = AnsiblePlayer.Play(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.False(taskResult.Success);

            //-----------------------------------------------------------------
            // Verify that the module detects unknown secret arguments.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: absent
        image: {serviceImage}
        secret:
          - source: secret-1
            target: secret
            uid: {TestHelper.TestUID}
            gid: {TestHelper.TestGID}
            mode: 440
            UNKNOWN: argument
";
            results = AnsiblePlayer.Play(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.False(taskResult.Success);

            //-----------------------------------------------------------------
            // Verify that the module detects unknown mount arguments.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: absent
        image: {serviceImage}
        mount:
          - type: volume
            source: test-volume
            target: /mnt/volume
            readonly: no
            volume_label:
              - VOLUME=TEST
            volume_nocopy: yes
            UNKNOWN: argument
";
            results = AnsiblePlayer.Play(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.False(taskResult.Success);

            //-----------------------------------------------------------------
            // Verify that the module detects unknown publish arguments.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: absent
        image: {serviceImage}
        publish:
          - published: 8080
            target: 80
            UNKNOWN: argument
";
            results = AnsiblePlayer.Play(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.False(taskResult.Success);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Remove()
        {
            DeployTestService();

            // Verify that [state=absent] removes the service.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: absent
";
            var results = AnsiblePlayer.Play(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Empty(cluster.ListServices().Where(s => s.Name == serviceName));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create_Remove_NoChange()
        {
            // Verify that [state=absent] does nothing when no service exists.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: absent
        image: neoncluster/test
";
            var results = AnsiblePlayer.Play(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Empty(cluster.ListServices().Where(s => s.Name == serviceName));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Rollback()
        {
            // Create the initial service.

            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: present
        image: neoncluster/test:0
";
            var results = AnsiblePlayer.Play(playbook);
            var taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == serviceName));

            var details = cluster.InspectService(serviceName);

            Assert.Equal("neoncluster/test:0", details.Spec.TaskTemplate.ContainerSpec.ImageWithoutSHA);

            // Verify that rolling back a service with no previous state
            // changes nothing.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: rollback
";
            results = AnsiblePlayer.Play(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == serviceName));

            details = cluster.InspectService(serviceName);

            Assert.Equal("neoncluster/test:0", details.Spec.TaskTemplate.ContainerSpec.ImageWithoutSHA);

            // Update the service to use a new image.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: present
        image: neoncluster/test:1
";
            results = AnsiblePlayer.Play(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == serviceName));

            details = cluster.InspectService(serviceName);

            Assert.Equal("neoncluster/test:1", details.Spec.TaskTemplate.ContainerSpec.ImageWithoutSHA);

            // Rollback the service and verify the original image.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: manage service
      neon_docker_service:
        name: {serviceName}
        state: rollback
";
            results = AnsiblePlayer.Play(playbook);
            taskResult = results.GetTaskResult("manage service");

            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListServices().Where(s => s.Name == serviceName));

            details = cluster.InspectService(serviceName);

            Assert.Equal("neoncluster/test:0", details.Spec.TaskTemplate.ContainerSpec.ImageWithoutSHA);
        }
    }
}
